using Godot;
using System;
using GodotEnvironment = Godot.Environment;

namespace Undergang.Services;

/// <summary>
/// Manages professional lighting setup for the game including:
/// - WorldEnvironment with sky and ambient lighting
/// - Advanced DirectionalLight settings
/// - Post-processing effects (SSAO, SSIL, Glow, etc.)
/// </summary>
public partial class LightingManager : Node3D
{
    [ExportGroup("Lighting Quality")]
    [Export] public bool EnableSSAO { get; set; } = true;
    [Export] public bool EnableSSIL { get; set; } = true;
    [Export] public bool EnableGlow { get; set; } = true;
    [Export(PropertyHint.Range, "0,2,0.1")] public float GlobalLightingIntensity { get; set; } = 1.0f;

    [ExportGroup("Ambient Lighting")]
    [Export] public Color AmbientLightColor { get; set; } = new Color(0.4f, 0.5f, 0.6f);
    [Export(PropertyHint.Range, "0,2,0.1")] public float AmbientLightEnergy { get; set; } = 0.3f;

    [ExportGroup("Sun Settings")]
    [Export] public Color SunColor { get; set; } = new Color(1.0f, 0.95f, 0.85f);
    [Export(PropertyHint.Range, "0,4,0.1")] public float SunEnergy { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,90,1")] public float SunAngleX { get; set; } = 45.0f;
    [Export(PropertyHint.Range, "-180,180,1")] public float SunAngleY { get; set; } = -45.0f;

    private WorldEnvironment _worldEnvironment;
    private GodotEnvironment _environment;
    private DirectionalLight3D _directionalLight;

    public override void _Ready()
    {
        SetupWorldEnvironment();
        SetupDirectionalLight();
        ApplyPostProcessing();

    }

    private void SetupWorldEnvironment()
    {
        // Create or find WorldEnvironment
        _worldEnvironment = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        if (_worldEnvironment == null)
        {
            _worldEnvironment = new WorldEnvironment();
            _worldEnvironment.Name = "WorldEnvironment";
            AddChild(_worldEnvironment);
        }

        // Create new Environment
        _environment = new GodotEnvironment();
        _worldEnvironment.Environment = _environment;

        // Background/Sky setup - using a simple color for now
        _environment.BackgroundMode = GodotEnvironment.BGMode.Sky;

        var sky = new Sky();
        var skyMaterial = new ProceduralSkyMaterial();

        // Professional sky settings for tactical game
        skyMaterial.SkyTopColor = new Color(0.385f, 0.454f, 0.55f);
        skyMaterial.SkyHorizonColor = new Color(0.646f, 0.656f, 0.67f);
        skyMaterial.GroundBottomColor = new Color(0.1f, 0.1f, 0.1f);
        skyMaterial.GroundHorizonColor = new Color(0.37f, 0.33f, 0.31f);
        skyMaterial.SunAngleMax = 30.0f;
        skyMaterial.SunCurve = 0.15f;

        sky.SkyMaterial = skyMaterial;
        _environment.Sky = sky;

        // Ambient lighting - crucial for filling in shadows
        _environment.AmbientLightSource = GodotEnvironment.AmbientSource.Sky;
        _environment.AmbientLightColor = AmbientLightColor;
        _environment.AmbientLightSkyContribution = 0.5f;
        _environment.AmbientLightEnergy = AmbientLightEnergy;

        // Reflected light - adds realism
        _environment.ReflectedLightSource = GodotEnvironment.ReflectionSource.Sky;

        // Tonemap for better color range
        _environment.TonemapMode = GodotEnvironment.ToneMapper.Filmic;
        _environment.TonemapExposure = 1.0f;
        _environment.TonemapWhite = 1.0f;

    }

    private void SetupDirectionalLight()
    {
        // Find existing DirectionalLight or create new one
        var mainScene = GetParent();
        _directionalLight = mainScene?.GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");

        if (_directionalLight == null)
        {
            _directionalLight = new DirectionalLight3D();
            _directionalLight.Name = "DirectionalLight3D";
            mainScene?.AddChild(_directionalLight);
        }

        // Professional sun light settings
        _directionalLight.LightColor = SunColor;
        _directionalLight.LightEnergy = SunEnergy * GlobalLightingIntensity;
        _directionalLight.LightIndirectEnergy = 1.0f;
        _directionalLight.LightVolumetricFogEnergy = 1.0f;

        // Shadow settings for quality
        _directionalLight.ShadowEnabled = true;
        _directionalLight.ShadowOpacity = 0.6f; // Softer shadows
        _directionalLight.ShadowBlur = 2.0f; // Soft shadow edges

        // Directional shadow settings
        _directionalLight.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        _directionalLight.DirectionalShadowMaxDistance = 100.0f;
        _directionalLight.DirectionalShadowBlendSplits = true;
        _directionalLight.DirectionalShadowFadeStart = 0.8f;

        // Position the sun at specified angles
        var radX = Mathf.DegToRad(SunAngleX);
        var radY = Mathf.DegToRad(SunAngleY);
        _directionalLight.RotationDegrees = new Vector3(SunAngleX, SunAngleY, 0);

    }

    private void ApplyPostProcessing()
    {
        if (_environment == null) return;

        // SSAO (Screen Space Ambient Occlusion) - adds depth perception
        if (EnableSSAO)
        {
            _environment.SsaoEnabled = true;
            _environment.SsaoRadius = 2.0f;
            _environment.SsaoIntensity = 2.0f;
            _environment.SsaoPower = 1.5f;
            _environment.SsaoDetail = 0.5f;
            _environment.SsaoHorizon = 0.06f;
            _environment.SsaoSharpness = 0.98f;
        }

        // SSIL (Screen Space Indirect Lighting) - adds bounce light
        if (EnableSSIL)
        {
            _environment.SsilEnabled = true;
            _environment.SsilRadius = 5.0f;
            _environment.SsilIntensity = 1.0f;
            _environment.SsilSharpness = 0.98f;
            _environment.SsilNormalRejection = 1.0f;
        }

        // Glow/Bloom for highlights
        if (EnableGlow)
        {
            _environment.GlowEnabled = true;
            _environment.GlowNormalized = true;
            _environment.GlowIntensity = 0.5f;
            _environment.GlowStrength = 0.8f;
            _environment.GlowBloom = 0.1f;
            _environment.GlowBlendMode = GodotEnvironment.GlowBlendModeEnum.Softlight;
            _environment.GlowHdrThreshold = 1.0f;
            _environment.GlowHdrScale = 2.0f;
        }

        // Adjustments for better color
        _environment.AdjustmentEnabled = true;
        _environment.AdjustmentBrightness = 1.0f;
        _environment.AdjustmentContrast = 1.1f;
        _environment.AdjustmentSaturation = 1.05f;

    }

    /// <summary>
    /// Update lighting intensity in real-time
    /// </summary>
    public void SetGlobalIntensity(float intensity)
    {
        GlobalLightingIntensity = Mathf.Clamp(intensity, 0.0f, 2.0f);
        if (_directionalLight != null)
        {
            _directionalLight.LightEnergy = SunEnergy * GlobalLightingIntensity;
        }
    }

    /// <summary>
    /// Change ambient light color and energy
    /// </summary>
    public void SetAmbientLight(Color color, float energy)
    {
        AmbientLightColor = color;
        AmbientLightEnergy = energy;
        if (_environment != null)
        {
            _environment.AmbientLightColor = color;
            _environment.AmbientLightEnergy = energy;
        }
    }

    /// <summary>
    /// Change sun color and energy
    /// </summary>
    public void SetSunLight(Color color, float energy)
    {
        SunColor = color;
        SunEnergy = energy;
        if (_directionalLight != null)
        {
            _directionalLight.LightColor = color;
            _directionalLight.LightEnergy = energy * GlobalLightingIntensity;
        }
    }

    /// <summary>
    /// Rotate the sun to different angles
    /// </summary>
    public void SetSunAngle(float angleX, float angleY)
    {
        SunAngleX = angleX;
        SunAngleY = angleY;
        if (_directionalLight != null)
        {
            _directionalLight.RotationDegrees = new Vector3(angleX, angleY, 0);
        }
    }
}
