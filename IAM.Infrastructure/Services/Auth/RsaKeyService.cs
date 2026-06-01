using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace IAM.Infrastructure.Services.Auth;

/// <summary>
/// Service for managing RSA keys used for JWT signing.
/// Generates and exports keys in JWKS format for external validation.
/// </summary>
public class RsaKeyService
{
    private RSA? _rsa;
    private readonly string _keyPath;
    private const string PrivateKeyFileName = "private_key.pem";
    private const string PublicKeyFileName = "public_key.pem";

    public RsaKeyService(string keyStorePath = "keys")
    {
        _keyPath = keyStorePath;
        _rsa = null;
        
        // Ensure directory exists
        if (!Directory.Exists(_keyPath))
            Directory.CreateDirectory(_keyPath);

        // Load or generate keys
        var privateKeyPath = Path.Combine(_keyPath, PrivateKeyFileName);
        
        if (File.Exists(privateKeyPath))
        {
            LoadKeysFromFile(privateKeyPath);
        }
        else
        {
            GenerateAndSaveKeys();
        }

        // Ensure RSA was initialized
        if (_rsa == null)
            throw new InvalidOperationException("Failed to initialize RSA keys");

        // Ensure JWKS exists
        var jwksPath = Path.Combine(_keyPath, "jwks.json");
        if (!File.Exists(jwksPath))
        {
            var jwksJson = System.Text.Json.JsonSerializer.Serialize(GetJwks());
            File.WriteAllText(jwksPath, jwksJson);
        }
    }

    /// <summary>
    /// Generates new RSA key pair and saves to disk.
    /// </summary>
    private void GenerateAndSaveKeys()
    {
        _rsa = RSA.Create(2048);
        // we are using the 2048 bits key size which is considered secure for JWT signing, it provides a good balance between security and performance. For higher security requirements, you can use 4096 bits, but it will be slower to generate and sign tokens.

        var privateKeyPath = Path.Combine(_keyPath, PrivateKeyFileName);
        var publicKeyPath = Path.Combine(_keyPath, PublicKeyFileName);

        // Save private key
        var privateKeyBytes = _rsa.ExportRSAPrivateKey();
        File.WriteAllBytes(privateKeyPath, privateKeyBytes);

        // Save public key
        var publicKeyBytes = _rsa.ExportRSAPublicKey();
        File.WriteAllBytes(publicKeyPath, publicKeyBytes);

        // Save JWKS
        var jwksPath = Path.Combine(_keyPath, "jwks.json");
        var jwks = GetJwks();
        var jwksJson = System.Text.Json.JsonSerializer.Serialize(jwks);
        File.WriteAllText(jwksPath, jwksJson);
    }

    /// <summary>
    /// Loads RSA key from file.
    /// </summary>
    private void LoadKeysFromFile(string privateKeyPath)
    {
        var privateKeyBytes = File.ReadAllBytes(privateKeyPath);
        _rsa = RSA.Create();
        _rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
    }

    /// <summary>
    /// Gets the RSA private key for signing.
    /// </summary>
    public RsaSecurityKey GetPrivateKey()
    {
        if (_rsa == null)
            throw new InvalidOperationException("RSA key not initialized");
        return new RsaSecurityKey(_rsa) { KeyId = "grafana-jwt-key" };
    }

    /// <summary>
    /// Gets the RSA public key for verification.
    /// </summary>
    public RsaSecurityKey GetPublicKey()
    {
        if (_rsa == null)
            throw new InvalidOperationException("RSA key not initialized");
        return new RsaSecurityKey(_rsa.ExportParameters(false));
    }

    /// <summary>
    /// Exports the public key in JWKS format for consumption by external systems (e.g., Grafana).
    /// </summary>
    public JsonWebKeySet GetJwks()
    {
        if (_rsa == null)
            throw new InvalidOperationException("RSA key not initialized");

        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(_rsa.ExportRSAPublicKey(), out _);

        var publicKey = new RsaSecurityKey(rsa.ExportParameters(false))
        {
            KeyId = "grafana-jwt-key"
        };

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jwk.Alg = "RS256";
        jwk.Use = "sig";

        // Create JsonWebKeySet with keys
        var keySet = new JsonWebKeySet();
        keySet.Keys.Add(jwk);
        return keySet;
    }
}

/// <summary>
/// DTO for JWKS response that matches standard format.
/// </summary>
[Serializable]
public class JwksResponse
{
    [JsonPropertyName("keys")]
    public List<JwkKey> Keys { get; set; } = new();
}

/// <summary>
/// Represents a single key in the JWKS format.
/// </summary>
[Serializable]
public class JwkKey
{
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = "RSA";

    [JsonPropertyName("use")]
    public string Use { get; set; } = "sig";

    [JsonPropertyName("kid")]
    public string Kid { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; set; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; set; } = string.Empty;

    [JsonPropertyName("alg")]
    public string Alg { get; set; } = "RS256";
}
