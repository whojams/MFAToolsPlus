using MFAToolsPlus.Helper;
using System;
using System.Security.Cryptography;
using System.Text;
using ProtectedData = CrossPlatformProtectedData.ProtectedData;


namespace MFAToolsPlus.Helper;

public static class SimpleEncryptionHelper
{

    // 加密（自动绑定设备）
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        try
        {
            var data = Encoding.UTF8.GetBytes(plainText);
            var wEncryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(wEncryptedData);
        }
        catch (Exception e)
        {
            LoggerHelper.Warn("跨平台数据加密失败: " + e.Message);
            return plainText;
        }
    }

    // 解密（仅当前设备可用）
    public static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
            return string.Empty;
        string result;

        try
        {
            var data = Convert.FromBase64String(encryptedBase64);
            var decryptedData = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            result = Encoding.UTF8.GetString(decryptedData);
            if (string.IsNullOrWhiteSpace(result))
                throw new Exception("result is null");
            return result;
        }
        catch (Exception e)
        {
            LoggerHelper.Warn("跨平台数据解密失败: " + e.Message);
            return encryptedBase64;
        }
    }
}
