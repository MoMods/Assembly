using StackExchange.Redis;

namespace Assembly.Helpers.Database.Models
{
    public interface IDatabaseConnector
    {
        bool TestConnection();
        void UploadToDatabase(string key, byte[] value, string keyModifier = null);
        object DownloadFromDatabase(string key, string keyModifier = null);
        void ClearDatabaseContents(string keyModifier);

    }
}
