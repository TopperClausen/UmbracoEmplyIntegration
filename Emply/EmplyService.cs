using System;
using System.Collections.Generic;
using System.Linq;
using Limbo.Integrations.Emply;
using Limbo.Integrations.Emply.Extensions;
using Limbo.Integrations.Emply.Models.Jobs;
using Limbo.Integrations.Emply.Models.Postings;
using Limbo.Integrations.Emply.Options.Postings;
using Limbo.Integrations.Emply.Responses.Postings;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using System.Web;
using Newtonsoft.Json.Linq;
using Skybrud.Essentials.Security;
using umbraco_emply.Models;

namespace umbraco_emply.Integrations.Emply
{
    public class EmplyService
    {
        private IContentService _contentService;
        private EmplyHttpService _emplyService;
        private string newGridJson;
        private LimboOptions _options;
        private List<IContent> _modifyedData = new List<IContent>();

        public EmplyService(IContentService contentService, IOptions<LimboOptions> options)
        {
            if (contentService == null) throw new ArgumentException(nameof(contentService));

            this._contentService = contentService;

            this._emplyService = EmplyHttpService.CreateFromApiKey(options.Value.Emply.CustomerName, options.Value.Emply.ApiKey);

            this._options = options.Value;
        }

        public void Import() {
            Dictionary<string, IContent> existingJobs = _contentService
                .GetPagedChildren(Constants.JobPageID, 0, int.MaxValue, out long _)
                .Where(x => !string.IsNullOrWhiteSpace(x.GetValue<string>("emplyId")))
                .ToDictionary(x => x.GetValue<string>("emplyId"));

            EmplyPostingListResponse response = _emplyService.GetPostings(new EmplyGetPostingsOptions
            {
                MediaId = this._options.Emply.MediaId,
                Take = 500
            });

            List<EmplyPosting> changedJobs = new List<EmplyPosting>();

            foreach(EmplyPosting posting in response.Body)
            {
                if (!existingJobs.TryGetValue(posting.VacancyId.ToString(), out IContent content)) AddNewJob(posting);
                else if (this.isChanged(posting, content)) ModifyJob(content, posting);
            }
            SaveAndPublishChanges();
        }

        private Boolean isChanged(EmplyPosting newNode, IContent oldNode) {
            
            // Obtaining variables for comparison
            string newGridJson = GetGridContent(newNode)?.ToString();
            newNode.Data.TryGet(this._options.Emply.Category, out EmplyJobDataType1 categoryField);
            string title = newNode.Advertisements.FirstOrDefault(x => x.IsDefault)?.Title.ToString() ?? newNode.Title.ToString();
            string categoryName = categoryField?.Value[0].Title.ToString();
            string categoryId = categoryField?.Value[0].ToString();
            string placeOfEmployment = newNode.Location?.Address ?? string.Empty;
            DateTime deadline = newNode.DeadlineUtc?.ToLocalTime().DateTimeOffset.DateTime ?? new DateTime(1753, 1, 1);
            string adUrl = newNode.AdUrl?.ToString() ?? string.Empty;
            string applyUrl = newNode.ApplyUrl?.ToString() ?? string.Empty;
            string applicationLink = string.IsNullOrWhiteSpace(adUrl) ? applyUrl : adUrl;
            string website = adUrl;

            string umbracoName = $"{title} ({newNode.JobId})";
            
            bool hasChanges = false;

            // checking for changes
            if (title != oldNode.GetValue<string>("title")) hasChanges = true;
            if (categoryName != oldNode.GetValue<string>("category")) hasChanges = true;
            if (categoryId != oldNode.GetValue<string>("categoryID")) hasChanges = true;
            if (placeOfEmployment != oldNode.GetValue<string>("placeOfEmployment")) hasChanges = true;                
            if (website != oldNode.GetValue<string>("website")) hasChanges = true;
            if (deadline != oldNode.GetValue<DateTime>("jobDeadline")) hasChanges = true;
            if (applicationLink != oldNode.GetValue<string>("applicationLink")) hasChanges = true;
            if (newGridJson != oldNode.GetValue<string>("grid")) hasChanges = true;

            if (oldNode.Name != umbracoName)
            {
                oldNode.Name = umbracoName;
                hasChanges = true;
            }

            return hasChanges;
        }

        private void AddNewJob(EmplyPosting content) {
            string title = content.Advertisements.FirstOrDefault(x => x.IsDefault)?.Title.ToString() ?? content.Title.ToString();
            var node = this._contentService.Create(title, Constants.JobPageKey, this._options.Emply.TemplateAlias);

            ModifyJob(node, content);
        }

        private void SaveAndPublishChanges() {
            foreach (IContent content in this._modifyedData)
            {
                this._contentService.SaveAndPublish(content);
                this._contentService.Delete(content);
            }
        }

        private void ModifyJob(IContent job, EmplyPosting newData) {
            if(this._options is null) throw new Exception("A");
            if(this._options.Emply is null) throw new Exception("B");
            if(this._options.Emply.Category is null) throw new Exception("C");

            newData.Data.TryGet(this._options.Emply.Category, out EmplyJobDataType1 categoryField);

            string title = newData.Advertisements.FirstOrDefault(x => x.IsDefault)?.Title.ToString() ?? newData.Title.ToString();
            string categoryName = categoryField?.Value[0].Title.ToString();
            string categoryId = categoryField?.Value[0].ToString();
            string placeOfEmployment = newData.Location?.Address ?? string.Empty;
            DateTime deadline = newData.DeadlineUtc?.ToLocalTime().DateTimeOffset.DateTime ?? new DateTime(1753, 1, 1);
            string adUrl = newData.AdUrl?.ToString() ?? string.Empty;
            string applyUrl = newData.ApplyUrl?.ToString() ?? string.Empty;
            string applicationLink = string.IsNullOrWhiteSpace(adUrl) ? applyUrl : adUrl;
            string website = adUrl;

            string newGridJson = GetGridContent(newData)?.ToString();

            string umbracoName = $"{title} ({newData.JobId})";

            job.Name = umbracoName;

            job.SetValue("emplyId", newData.VacancyId.ToString());
            job.SetValue("title", title);
            job.SetValue("category", categoryName);
            job.SetValue("categoryID", categoryId);
            job.SetValue("placeOfEmployment", placeOfEmployment);
            job.SetValue("website", website);
            job.SetValue("jobDeadline", deadline);
            job.SetValue("applicationLink", applicationLink);

            job.SetValue("grid", newGridJson);

            this._modifyedData.Add(job);
        }
    
        private JObject GetGridContent(EmplyPosting posting){
            // Get the content of the default advertisement (if any)
            string html = posting.Advertisements.FirstOrDefault(x => x.IsDefault)?.Content.ToString();
            if (string.IsNullOrWhiteSpace(html)) return null;

            // Decode the HTML (not sure if maybe encoded in the new API)
            html = HttpUtility.HtmlDecode(html);

            // Enclose the value in paragraph tags if it isn't already
            if (!html.StartsWith("<p>") && !html.EndsWith("</p>")) html = $"<p>{html}</p>";

            // If we generate random GUID keys for the grid model, we won't be able to later compare the old and new
            // values properly. Bit of a hack, but it does the job :D
            Guid key1 = SecurityUtils.GetMd5Guid((posting.JobId * 1000000 + 1).ToString());
            Guid key2 = SecurityUtils.GetMd5Guid((posting.JobId * 1000000 + 2).ToString());

            JObject controlsValueProperties = new JObject {
                {"rte", html },
                {"title", posting.Advertisements.FirstOrDefault(x => x.IsDefault)?.Title.ToString() ?? string.Empty }
            };

            JArray controlsValue = new JArray {
                new JObject {
                    {"key", key2},
                    {"contentType", "6c8c8b9b-b57d-4987-b1b1-436b96055a81"},
                    {"properties", controlsValueProperties}
                }
            };

            JArray controls = new JArray {
                new JObject {
                    {"value", controlsValue },
                    {"editor", new JObject {
                        { "alias", "tkTextGrid" },
                        { "view", "/App_Plugins/Skybrud.Umbraco.Elements/Views/Grid.html" }
                    }},
                    {"styles", null },
                    {"config", null }
                }
            };

            JArray areas = new JArray {
                new JObject {
                    {"grid", 12},
                    {"styles", null},
                    {"config", null},
                    {"controls", controls}
                }
            };

            JArray rows = new JArray {
                new JObject {
                    {"name", "sectionOneCol"},
                    {"id", key1},
                    {"areas", areas},
                    {"styles", null},
                    {"config", null}
                }
            };

            JArray sections = new JArray {
                new JObject {
                    {"grid", 12}, {"rows", rows}
                }
            };

            JObject grid = new JObject {
                {"name", "contentArea" },
                {"sections", sections }
            };

            return grid;

        }
    
    }
}
