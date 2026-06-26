
namespace EIMSNext.Auth
{
    public class Constants
    {
        public const int TokenLifetime_Default = 28800;
        public const string NoPassword = "(!@#^&*$%) [,./';:>?<]";
    }

    public class CustomScope
    {
        public const string Read = "read";
        public const string ReadWrite = "readwrite";
    }
}