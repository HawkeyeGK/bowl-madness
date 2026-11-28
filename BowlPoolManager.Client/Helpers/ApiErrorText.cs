namespace BowlPoolManager.Client.Helpers
{
    /// <summary>
    /// Turns a failed API response into text an admin can act on: the server's own message when it sent one,
    /// otherwise the status code (issue 138: admin saves used to fail without saying anything).
    /// </summary>
    public static class ApiErrorText
    {
        private const int MaxMessageLength = 500;

        public static async Task<string> FromAsync(HttpResponseMessage response)
        {
            string body;
            try
            {
                body = (await response.Content.ReadAsStringAsync()).Trim();
            }
            catch
            {
                body = string.Empty;
            }

            // An empty body, or a gateway's HTML error page, says nothing useful: fall back to the status code.
            var useful = body.Length > 0 && body.Length <= MaxMessageLength && !body.StartsWith('<');
            return useful
                ? body
                : $"The request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
        }

        /// <summary>Throws an HttpRequestException carrying the server's message when the response is not a success.</summary>
        public static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            throw new HttpRequestException(await FromAsync(response), null, response.StatusCode);
        }
    }
}
