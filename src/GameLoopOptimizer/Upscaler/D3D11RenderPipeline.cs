using System.Numerics;
using System.Runtime.InteropServices;
using GameLoopOptimizer.Core;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace GameLoopOptimizer.Upscaler;

/// <summary>
/// Direct3D 11 rendering pipeline for FSR 1.0 spatial upscaling.
/// Manages device creation, shader compilation, and the two-pass render loop
/// (EASU upscale → RCAS sharpening).
/// </summary>
public sealed class D3D11RenderPipeline : IDisposable
{
    // D3D11 Core
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _backbufferRtv;

    // Shaders
    private ID3D11VertexShader? _fullscreenVS;
    private ID3D11PixelShader? _easuPS;
    private ID3D11PixelShader? _rcasPS;
    private ID3D11PixelShader? _passthroughPS;

    // Resources
    private ID3D11SamplerState? _linearSampler;
    private ID3D11SamplerState? _pointSampler;
    private ID3D11Buffer? _easuConstantBuffer;
    private ID3D11Buffer? _rcasConstantBuffer;

    // Intermediate render targets
    private ID3D11Texture2D? _easuTarget;
    private ID3D11RenderTargetView? _easuRtv;
    private ID3D11ShaderResourceView? _easuSrv;

    // Captured frame SRV
    private ID3D11ShaderResourceView? _inputSrv;
    private ID3D11Texture2D? _lastInputTexture;

    private uint _outputWidth;
    private uint _outputHeight;
    private bool _disposed;

    public ID3D11Device? Device => _device;
    public ID3D11DeviceContext? Context => _context;
    public bool IsInitialized { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EasuConstants
    {
        public Vector4 Const0;
        public Vector4 Const1;
        public Vector4 Const2;
        public Vector4 Const3;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RcasConstants
    {
        public Vector4 RcasConst;
    }

    /// <summary>
    /// Initialize the D3D11 device, swap chain, and compile all shaders.
    /// </summary>
    public bool Initialize(IntPtr overlayHwnd, int outputWidth, int outputHeight)
    {
        _outputWidth = (uint)outputWidth;
        _outputHeight = (uint)outputHeight;

        try
        {
            // Create D3D11 device
            var featureLevels = new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
            };

            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                featureLevels,
                out _device,
                out var featureLevel,
                out _context);

            if (_device == null || _context == null)
            {
                Logger.Error("D3D11Pipeline", "Failed to create D3D11 device.");
                return false;
            }

            Logger.Info("D3D11Pipeline", $"Created D3D11 device (Feature Level: {featureLevel})");

            // Create swap chain with multi-tier fallback:
            // 1. FlipDiscard with AlphaMode.Ignore (best performance on Windows 10/11)
            // 2. FlipSequential with AlphaMode.Ignore
            // 3. FlipDiscard with AlphaMode.Unspecified
            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var swapEffectsToTry = new[] { SwapEffect.FlipDiscard, SwapEffect.FlipSequential };
            Exception? lastEx = null;

            foreach (var effect in swapEffectsToTry)
            {
                try
                {
                    var swapDesc = new SwapChainDescription1
                    {
                        Width = _outputWidth,
                        Height = _outputHeight,
                        Format = Format.B8G8R8A8_UNorm,
                        Stereo = false,
                        SampleDescription = new SampleDescription(1, 0),
                        BufferUsage = Usage.RenderTargetOutput,
                        BufferCount = 2,
                        Scaling = Scaling.Stretch,
                        SwapEffect = effect,
                        AlphaMode = AlphaMode.Ignore,
                        Flags = SwapChainFlags.None
                    };

                    _swapChain = factory.CreateSwapChainForHwnd(_device, overlayHwnd, swapDesc);
                    Logger.Info("D3D11Pipeline", $"Created SwapChain using {effect} (AlphaMode.Ignore)");
                    break;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Logger.Warn("D3D11Pipeline", $"SwapChain {effect} (Ignore) failed: {ex.Message}");
                }
            }

            if (_swapChain == null)
            {
                try
                {
                    var swapDesc = new SwapChainDescription1
                    {
                        Width = _outputWidth,
                        Height = _outputHeight,
                        Format = Format.B8G8R8A8_UNorm,
                        Stereo = false,
                        SampleDescription = new SampleDescription(1, 0),
                        BufferUsage = Usage.RenderTargetOutput,
                        BufferCount = 2,
                        Scaling = Scaling.Stretch,
                        SwapEffect = SwapEffect.FlipDiscard,
                        AlphaMode = AlphaMode.Unspecified,
                        Flags = SwapChainFlags.None
                    };

                    _swapChain = factory.CreateSwapChainForHwnd(_device, overlayHwnd, swapDesc);
                    Logger.Info("D3D11Pipeline", "Created SwapChain using FlipDiscard (AlphaMode.Unspecified)");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"All SwapChain creation attempts failed: {ex.Message}", ex);
                }
            }

            // Disable Alt+Enter fullscreen toggle
            factory.MakeWindowAssociation(overlayHwnd, WindowAssociationFlags.IgnoreAltEnter);

            CreateBackbufferRtv();
            CompileShaders();
            CreateSamplers();
            CreateConstantBuffers();

            IsInitialized = true;
            Logger.Success("D3D11Pipeline", $"Pipeline initialized: {outputWidth}×{outputHeight}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("D3D11Pipeline", $"Pipeline init failed: {ex.Message}");
            return false;
        }
    }

    private void CreateBackbufferRtv()
    {
        _backbufferRtv?.Dispose();
        using var backbuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _backbufferRtv = _device!.CreateRenderTargetView(backbuffer);
    }

    private void CompileShaders()
    {
        // Vertex shader
        Compiler.Compile(FsrShaderSource.FullscreenVS, "main", "fullscreen_vs", "vs_5_0",
            out var vsBlob, out var vsError);
        if (vsBlob == null)
            throw new InvalidOperationException($"VS compilation failed: {vsError?.AsString()}");
        _fullscreenVS = _device!.CreateVertexShader(vsBlob.AsBytes());
        vsBlob.Dispose();

        // EASU pixel shader
        Compiler.Compile(FsrShaderSource.EasuPS, "main", "easu_ps", "ps_5_0",
            out var easuBlob, out var easuError);
        if (easuBlob == null)
            throw new InvalidOperationException($"EASU PS compilation failed: {easuError?.AsString()}");
        _easuPS = _device.CreatePixelShader(easuBlob.AsBytes());
        easuBlob.Dispose();

        // RCAS pixel shader
        Compiler.Compile(FsrShaderSource.RcasPS, "main", "rcas_ps", "ps_5_0",
            out var rcasBlob, out var rcasError);
        if (rcasBlob == null)
            throw new InvalidOperationException($"RCAS PS compilation failed: {rcasError?.AsString()}");
        _rcasPS = _device.CreatePixelShader(rcasBlob.AsBytes());
        rcasBlob.Dispose();

        // Passthrough pixel shader
        Compiler.Compile(FsrShaderSource.PassthroughPS, "main", "passthrough_ps", "ps_5_0",
            out var ptBlob, out var ptError);
        if (ptBlob == null)
            throw new InvalidOperationException($"Passthrough PS compilation failed: {ptError?.AsString()}");
        _passthroughPS = _device.CreatePixelShader(ptBlob.AsBytes());
        ptBlob.Dispose();

        Logger.Info("D3D11Pipeline", "All FSR shaders compiled successfully.");
    }

    private void CreateSamplers()
    {
        _linearSampler = _device!.CreateSamplerState(new SamplerDescription
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MaxAnisotropy = 1,
            ComparisonFunc = ComparisonFunction.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue
        });

        _pointSampler = _device!.CreateSamplerState(new SamplerDescription
        {
            Filter = Filter.MinMagMipPoint,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MaxAnisotropy = 1,
            ComparisonFunc = ComparisonFunction.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue
        });
    }

    private void CreateConstantBuffers()
    {
        _easuConstantBuffer = _device!.CreateBuffer(new BufferDescription
        {
            ByteWidth = (uint)Marshal.SizeOf<EasuConstants>(),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ConstantBuffer,
            CPUAccessFlags = CpuAccessFlags.Write
        });

        _rcasConstantBuffer = _device!.CreateBuffer(new BufferDescription
        {
            ByteWidth = (uint)Marshal.SizeOf<RcasConstants>(),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ConstantBuffer,
            CPUAccessFlags = CpuAccessFlags.Write
        });
    }

    /// <summary>
    /// Ensures the intermediate EASU render target matches the output dimensions.
    /// </summary>
    private void EnsureEasuTarget()
    {
        if (_easuTarget != null)
        {
            var desc = _easuTarget.Description;
            if (desc.Width == _outputWidth && desc.Height == _outputHeight)
                return;
            _easuSrv?.Dispose();
            _easuRtv?.Dispose();
            _easuTarget.Dispose();
        }

        _easuTarget = _device!.CreateTexture2D(new Texture2DDescription
        {
            Width = _outputWidth,
            Height = _outputHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        });

        _easuRtv = _device.CreateRenderTargetView(_easuTarget);
        _easuSrv = _device.CreateShaderResourceView(_easuTarget);
    }

    /// <summary>
    /// Renders the upscaled frame using the two-pass FSR 1.0 pipeline.
    /// </summary>
    /// <param name="capturedFrame">The captured low-resolution frame texture.</param>
    /// <param name="inputWidth">Width of the captured frame.</param>
    /// <param name="inputHeight">Height of the captured frame.</param>
    /// <param name="sharpness">RCAS sharpness (0.0 = max sharpening, 1.0 = none).</param>
    public void RenderFrame(ID3D11Texture2D capturedFrame, int inputWidth, int inputHeight, float sharpness)
    {
        if (!IsInitialized || _context == null) return;

        EnsureEasuTarget();

        // Create SRV for captured frame if texture changed
        if (_lastInputTexture != capturedFrame)
        {
            _inputSrv?.Dispose();
            _inputSrv = _device!.CreateShaderResourceView(capturedFrame);
            _lastInputTexture = capturedFrame;
        }

        // Set common state
        _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _context.IASetInputLayout(null); // No input layout needed for fullscreen triangle
        _context.VSSetShader(_fullscreenVS);

        // ========== PASS 1: EASU (Upscale) ==========
        RenderEasuPass(inputWidth, inputHeight);

        // ========== PASS 2: RCAS (Sharpen) ==========
        RenderRcasPass(sharpness);

        // Present
        _swapChain!.Present(0); // VSync off for minimum latency
    }

    private void RenderEasuPass(int inputWidth, int inputHeight)
    {
        int outW = (int)_outputWidth;
        int outH = (int)_outputHeight;

        // Update EASU constants
        var easuConst = new EasuConstants
        {
            Const0 = new Vector4(inputWidth, inputHeight, 1.0f / inputWidth, 1.0f / inputHeight),
            Const1 = new Vector4(outW, outH, 1.0f / outW, 1.0f / outH),
            Const2 = new Vector4(
                (float)inputWidth / outW,
                (float)inputHeight / outH,
                0.5f * inputWidth / outW,
                0.5f * inputHeight / outH),
            Const3 = Vector4.Zero
        };

        var mapped = _context!.Map(_easuConstantBuffer!, 0, MapMode.WriteDiscard);
        Marshal.StructureToPtr(easuConst, mapped.DataPointer, false);
        _context.Unmap(_easuConstantBuffer!, 0);

        // Bind EASU pass
        var viewport = new Viewport(0, 0, outW, outH);
        _context.RSSetViewport(viewport);
        _context.OMSetRenderTargets(_easuRtv!);
        _context.PSSetShader(_easuPS);
        _context.PSSetConstantBuffer(0, _easuConstantBuffer);
        _context.PSSetShaderResource(0, _inputSrv!);
        _context.PSSetSampler(0, _linearSampler);

        // Draw fullscreen triangle
        _context.Draw(3, 0);

        // Unbind SRV to allow it as input in next pass
        _context.PSSetShaderResource(0, null!);
    }

    private void RenderRcasPass(float sharpness)
    {
        int outW = (int)_outputWidth;
        int outH = (int)_outputHeight;

        // Update RCAS constants
        var rcasConst = new RcasConstants
        {
            RcasConst = new Vector4(sharpness, 1.0f / outW, 1.0f / outH, 0)
        };

        var mapped = _context!.Map(_rcasConstantBuffer!, 0, MapMode.WriteDiscard);
        Marshal.StructureToPtr(rcasConst, mapped.DataPointer, false);
        _context.Unmap(_rcasConstantBuffer!, 0);

        // Bind RCAS pass — output to backbuffer
        var viewport = new Viewport(0, 0, outW, outH);
        _context.RSSetViewport(viewport);
        _context.OMSetRenderTargets(_backbufferRtv!);

        // Clear backbuffer with black
        _context.ClearRenderTargetView(_backbufferRtv!, new Color4(0, 0, 0, 1.0f));

        _context.PSSetShader(_rcasPS);
        _context.PSSetConstantBuffer(0, _rcasConstantBuffer);
        _context.PSSetShaderResource(0, _easuSrv!);
        _context.PSSetSampler(0, _pointSampler);

        _context.Draw(3, 0);

        _context.PSSetShaderResource(0, null!);
    }

    /// <summary>
    /// Resize the swap chain and intermediate targets when the output dimensions change.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0 || !IsInitialized) return;

        _outputWidth = (uint)newWidth;
        _outputHeight = (uint)newHeight;

        _backbufferRtv?.Dispose();
        _backbufferRtv = null;

        _swapChain?.ResizeBuffers(2, _outputWidth, _outputHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.None);

        CreateBackbufferRtv();

        // Force recreation of EASU target
        _easuSrv?.Dispose(); _easuSrv = null;
        _easuRtv?.Dispose(); _easuRtv = null;
        _easuTarget?.Dispose(); _easuTarget = null;

        Logger.Info("D3D11Pipeline", $"Resized to {newWidth}×{newHeight}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _inputSrv?.Dispose();
        _easuSrv?.Dispose();
        _easuRtv?.Dispose();
        _easuTarget?.Dispose();
        _easuConstantBuffer?.Dispose();
        _rcasConstantBuffer?.Dispose();
        _linearSampler?.Dispose();
        _pointSampler?.Dispose();
        _fullscreenVS?.Dispose();
        _easuPS?.Dispose();
        _rcasPS?.Dispose();
        _passthroughPS?.Dispose();
        _backbufferRtv?.Dispose();
        _swapChain?.Dispose();
        _context?.Dispose();
        _device?.Dispose();

        IsInitialized = false;
    }
}
