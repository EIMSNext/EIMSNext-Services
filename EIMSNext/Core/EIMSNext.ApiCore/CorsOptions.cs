namespace EIMSNext.ApiCore
{
    /// <summary>
    /// Cross-origin settings shared by API and static file responses.
    /// </summary>
    public class CorsOptions
    {
        public string[] AllowedOrigins { get; set; } =
        [
            "https://eimsnext.com",
            "https://www.eimsnext.com",
            "https://admin.eimsnext.com",
            "https://mobile.eimsnext.com",
            "http://localhost:*",
            "https://localhost:*"
        ];

    }
}
