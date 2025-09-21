namespace ITDoku.Services;
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string cipherBase64);
}
