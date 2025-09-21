using System.Security.Cryptography;
using System.Text;

namespace ITDoku.Services;

public class SecretProtector : ISecretProtector
{
    private readonly byte[] _key; // 32 Bytes (256 bit)

    public SecretProtector(IConfiguration cfg)
    {
        var b64 = cfg["Secrets:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(b64)) throw new InvalidOperationException("Missing Secrets:EncryptionKey");
        _key = Convert.FromBase64String(b64);
        if (_key.Length != 32) throw new InvalidOperationException("EncryptionKey must be 32 bytes (base64).");
    }

    public string Protect(string plaintext)
    {
        using var aes = new AesGcm(_key);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, pt, ct, tag);

        var all = new byte[12 + 16 + ct.Length];
        Buffer.BlockCopy(nonce, 0, all, 0, 12);
        Buffer.BlockCopy(tag, 0, all, 12, 16);
        Buffer.BlockCopy(ct, 0, all, 28, ct.Length);
        return Convert.ToBase64String(all);
    }

    public string Unprotect(string cipherBase64)
    {
        var all = Convert.FromBase64String(cipherBase64);
        var nonce = new byte[12]; var tag = new byte[16]; var ct = new byte[all.Length - 28];
        Buffer.BlockCopy(all, 0, nonce, 0, 12);
        Buffer.BlockCopy(all, 12, tag, 0, 16);
        Buffer.BlockCopy(all, 28, ct, 0, ct.Length);

        using var aes = new AesGcm(_key);
        var pt = new byte[ct.Length];
        aes.Decrypt(nonce, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }
}
