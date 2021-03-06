using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using AzureFunctions.Common;
using AzureFunctions.Contracts;
using AzureFunctions.Models;
using AzureFunctions.Trace;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json.Linq;

namespace AzureFunctions.Controllers
{
    public class AzureFunctionsController : ApiController
    {
        private readonly ITemplatesManager _templatesManager;
        private readonly ISettings _settings;
        private readonly IDiagnosticsManager _diagnosticsManager;
        private readonly TelemetryClient _telemetryClient;
        private readonly IPassThroughRequestManager _passThroughRequestManager;

        private Dictionary<string, string> _languageMap = new Dictionary<string, string>()
        {
            { "ja", "ja-JP"},
            { "ko", "ko-KR"},
            { "sv", "sv-SE"},
            { "cs", "cs-CZ"},
            { "zh-hans", "zh-CN"},
            { "zh-hant", "zh-TW"}
        };

        public AzureFunctionsController(ITemplatesManager templatesManager, ISettings settings, IDiagnosticsManager diagnosticsManager, TelemetryClient telemetryClient, IPassThroughRequestManager passThroughRequestManager)
        {
            _telemetryClient = telemetryClient;
            _templatesManager = templatesManager;
            _settings = settings;
            _diagnosticsManager = diagnosticsManager;
            _passThroughRequestManager = passThroughRequestManager;
        }

        [HttpGet]
        public HttpResponseMessage ListTemplates([FromUri] string runtime)
        {
            runtime = GetClearRuntime(runtime);

            return Request.CreateResponse(HttpStatusCode.OK, _templatesManager.GetTemplates(runtime));
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetBindingConfig([FromUri] string runtime)
        {
            runtime = GetClearRuntime(runtime);

            return Request.CreateResponse(HttpStatusCode.OK, await _templatesManager.GetBindingConfigAsync(runtime));
        }

        [HttpGet]
        public HttpResponseMessage GetLatestRuntime()
        {
            return Request.CreateResponse(HttpStatusCode.OK, Constants.CurrentLatestRuntimeVersion);
        }

        [HttpGet]
        public HttpResponseMessage GetResources([FromUri] string name, [FromUri] string runtime)
        {
            runtime = GetClearRuntime(runtime);
            string portalFolder = "";
            string sdkFolder = "";
            string languageFolder = name;

            if (name != "en")
            {
                if (!_languageMap.TryGetValue(name, out languageFolder))
                {
                    languageFolder = name + "-" + name;
                }
                portalFolder = Path.Combine(languageFolder, "AzureFunctions\\ResourcesPortal");
                sdkFolder = Path.Combine(languageFolder, "Resources");
            }

            List<string> resxFiles = new List<string>();
            var result = new JObject();
            string templateResourcesPath;

            if (!string.IsNullOrEmpty(portalFolder) && !string.IsNullOrEmpty(sdkFolder))
            {
                resxFiles.Add(Path.Combine(this._settings.ResourcesPortalPath.Replace(".Client", ""), portalFolder + "\\Resources.resx"));
                templateResourcesPath = Path.Combine(this._settings.TemplatesPath, runtime + "\\Resources\\" + sdkFolder + "\\Resources.resx");
                if (!File.Exists(templateResourcesPath))
                {
                    templateResourcesPath = Path.Combine(this._settings.TemplatesPath, "default\\Resources\\" + sdkFolder + "\\Resources.resx");
                }
                resxFiles.Add(templateResourcesPath);
                result["lang"] = ConvertResxToJObject(resxFiles);
                resxFiles.Clear();
            }

            // Always add english strings
            resxFiles.Add(Path.Combine(this._settings.ResourcesPortalPath.Replace(".Client", ""), "Resources.resx"));

            templateResourcesPath = Path.Combine(this._settings.TemplatesPath, runtime + "\\Resources\\Resources.resx");
            if (!File.Exists(templateResourcesPath))
            {
                templateResourcesPath = Path.Combine(this._settings.TemplatesPath, "default\\Resources\\Resources.resx");
            }
            resxFiles.Add(templateResourcesPath);
            result["en"] = ConvertResxToJObject(resxFiles);

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [Authorize]
        [HttpPost]
        public HttpResponseMessage ReportClientError([FromBody] ClientError clientError)
        {
            FunctionsTrace.Diagnostics.Error(new Exception(clientError.Message), TracingEvents.ClientError.Message, clientError.Message, clientError.StackTrace);
            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [Authorize]
        [HttpPost]
        public async Task<HttpResponseMessage> Diagnose(string armId)
        {
            var diagnosticResult = await _diagnosticsManager.Diagnose(armId);
            diagnosticResult = diagnosticResult.Where(r => !r.IsDiagnosingSuccessful || r.SuccessResult.HasUserAction);
            return Request.CreateResponse(HttpStatusCode.OK, diagnosticResult);
        }

        // HACK: this is temporary until ANT68
        [Authorize]
        [HttpGet]
        public async Task<HttpResponseMessage> GetRuntimeToken(string armId)
        {
            (var success, var runtimeTokenOrError) = await _diagnosticsManager.GetRuntimeToken(armId);

            return success
                ? Request.CreateResponse(HttpStatusCode.OK, runtimeTokenOrError)
                : Request.CreateErrorResponse(HttpStatusCode.InternalServerError, runtimeTokenOrError);
        }

        [HttpPost]
        public Task<HttpResponseMessage> PassThrough(RequestObject clientRequest)
        {
            return _passThroughRequestManager.HandleRequest(clientRequest);
        }

        [HttpPost]
        public async Task<HttpResponseMessage> TriggerFunctionAPIM(TriggerFunctionModel request)
        {
            var apimKey = Environment.GetEnvironmentVariable("APIMSubscriptionKey");
            if(String.IsNullOrEmpty(apimKey))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No APIMSubscriptionKey set");
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apimKey);
                client.DefaultRequestHeaders.Add("Ocp-Apim-Trace", "true");

                var content = new StringContent(request.body, Encoding.UTF8, "application/json");
                return await client.PostAsync(request.url, content);
            }
        }

        private JObject ConvertResxToJObject(List<string> resxFiles)
        {
            var jo = new JObject();

            foreach (var file in resxFiles)
            {
                if (File.Exists(file))
                {
                    // Create a ResXResourceReader for the file items.resx.
                    ResXResourceReader rsxr = new ResXResourceReader(file);

                    // Iterate through the resources and display the contents to the console.
                    foreach (DictionaryEntry d in rsxr)
                    {
                        jo[d.Key.ToString()] = d.Value.ToString();
                    }

                    //Close the reader.
                    rsxr.Close();
                }
            }

            return jo;
        }

        private string GetClearRuntime(string runtime)
        {
            return runtime.Replace("~", "");
        }
    }
}