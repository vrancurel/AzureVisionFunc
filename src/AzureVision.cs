using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;
using System;
using System.IO;
using System.Net.Http.Formatting;
using System.Text;
using Newtonsoft.Json;

namespace AzureVisionFunc
{
	public static class AzureVision
	{
		[FunctionName("AzureVision")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
		{
			// grab the key and URI from the portal config
			string visionKey = Environment.GetEnvironmentVariable("VisionKey");
			string visionUri = Environment.GetEnvironmentVariable("VisionUri");

			// create a client and request Tags for the image submitted
			VisionServiceClient vsc = new VisionServiceClient(visionKey, visionUri);
			VisualFeature[] vf = { VisualFeature.Tags };
			AnalysisResult result = null;
			string url = string.Empty;

                        log.Info("Calling Vision API");

			// if it's a POST method, we read the content as a byte array and assume it's an image
			if(req.Method.Method == "POST")
			{
				Stream stream = await req.Content.ReadAsStreamAsync();
				try
				{
					result = await vsc.AnalyzeImageAsync(stream, vf);
				}
      				catch (ClientException e)
                                {
                                        log.Info("Vision client error: " + e.Error.Message);
                                }
			}

			// else, if it's a GET method, we assume there's a URL on the query string, pointing to a valid image
			else if(req.Method.Method == "GET")
			{
				url = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "url", true) == 0).Value;
				try
				{
					result = await vsc.AnalyzeImageAsync(url, vf);
				}
				catch (ClientException e)
                                {
                                        log.Info("Vision client error: " + e.Error.Message);
                                }
			}

			// if we didn't get a result from the service, return a 400
			if(result == null)
				return req.CreateResponse(HttpStatusCode.BadRequest);

			return GetResponse(req, result.Tags);
		}

		private static HttpResponseMessage GetResponse(HttpRequestMessage req, Tag[] tagList)
		{
                        Dictionary<string, string> obj = new Dictionary<string, string>();

                        foreach(Tag t in tagList)
			{
                                obj.Add(t.Name, t.Confidence.ToString());
			}

			string json = JsonConvert.SerializeObject(obj);

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, JsonMediaTypeFormatter.DefaultMediaType.ToString())
			};
		}
	}
}