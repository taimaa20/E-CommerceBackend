namespace RestaurantPos.Api.Services.Printing.Agent
{
    /// <summary>
    /// Bound from the <c>"PrintAgent"</c> section of appsettings.json (or the
    /// <c>PrintAgent__*</c> environment variables). Drives the
    /// <see cref="LocalPrintAgentBackgroundService"/>.
    ///
    /// When <see cref="Enabled"/> is false (the default — and what the Azure
    /// App Service deployment uses), the agent is NOT registered in DI:
    /// no background service runs, no HTTP client is created, zero CPU,
    /// zero memory, zero behavioral change vs. before this feature shipped.
    ///
    /// When true (a single Windows PC inside the store running the same
    /// build artifact), the hosted service polls the cloud API every
    /// <see cref="PollIntervalSeconds"/> seconds, claims jobs whose printer
    /// has <c>UseLocalAgent = true</c>, prints to the LAN/USB printer, and
    /// reports the result back.
    /// </summary>
    public sealed class PrintAgentSettings
    {
        /// <summary>Master switch. False everywhere except on the store PC.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// HTTPS base URL of the cloud backend the agent polls.
        /// E.g. <c>https://sandobox.net</c>.
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Tenant id (X-Tenant-Id header).</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>POS user with Admin role used to log in (creates JWT).</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Password for the agent service account.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Default 2 — fast enough that tickets land while the customer is still at the counter.</summary>
        public int PollIntervalSeconds { get; set; } = 2;

        /// <summary>Max jobs claimed per tick. Default 5 keeps a single tick bounded.</summary>
        public int BatchSize { get; set; } = 5;
    }

    /// <summary>Minimal login response shape — only Token is required.</summary>
    public sealed class AgentLoginResponse
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
    }

    /// <summary>Body of POST /api/printing/agent/jobs/{id}/result.</summary>
    public sealed class AgentResultPayload
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
