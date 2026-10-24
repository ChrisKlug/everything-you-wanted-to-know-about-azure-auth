namespace Zkd.Demo.Azure.Auth.WhatReallyHappens;

// The web's registration. This app does what the web does, by hand, so it signs in as the web.
public class EntraOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(ClientId);

    public string Endpoint(string name) => $"{Instance}{TenantId}/oauth2/v2.0/{name}";
}
