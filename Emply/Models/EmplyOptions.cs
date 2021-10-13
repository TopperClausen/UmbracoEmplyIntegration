using System;
namespace umbraco_emply.Integrations.Emply.Models
{
    public class EmplyOptions
    {
        public string ApiKey { get; set; }
        public string CustomerName { get; set; }
        public string MediaId { get; set; }
        public string Category { get; set; }
        public string TemplateAlias { get; set; }
    }
}
