using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
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

namespace NotHotdogFunc
{
	public static class NotHotdog
	{
		[FunctionName("NotHotdog")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
		{
			// grab the key and URI from the portal config
			string visionKey = Environment.GetEnvironmentVariable("VisionKey");
			string visionUri = Environment.GetEnvironmentVariable("VisionUri");

			// create a client and request Tags for the image submitted
			VisionServiceClient vsc = new VisionServiceClient(visionKey, visionUri);
			VisualFeature[] vf = { VisualFeature.Tags };
			AnalysisResult result = null;
			string url = string.Empty;

			// if it's a POST method, we read the content as a byte array and assume it's an image
			if(req.Method.Method == "POST")
			{
				Stream stream = await req.Content.ReadAsStreamAsync();
				result = await vsc.AnalyzeImageAsync(stream, vf);
			}

			// else, if it's a GET method, we assume there's a URL on the query string, pointing to a valid image
			else if(req.Method.Method == "GET")
			{
				url = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "url", true) == 0).Value;
				result = await vsc.AnalyzeImageAsync(url, vf);
			}

			// if we didn't get a result from the service, return a 400
			if(result == null)
				return req.CreateResponse(HttpStatusCode.BadRequest);

			// otherwise, check to see if we got "hotdog" or "hot dog" in the tags (I've seen both)
			if(result.Tags.Select(tag => tag.Name.ToLowerInvariant()).Contains("hotdog"))
				return GetResponse(req, true);

			bool hot = false, dog = false;

			foreach(Tag t in result.Tags)
			{
				if(t.Name.ToLowerInvariant() == ("hot"))
					hot = true;
				else if(t.Name.ToLowerInvariant() == ("dog"))
					dog = true;

				if(hot && dog)
					return GetResponse(req, true);
			}

			return GetResponse(req, false);
		}

		private static HttpResponseMessage GetResponse(HttpRequestMessage req, bool hotdog)
		{
			var obj = new { isHotdog = hotdog.ToString().ToLowerInvariant() };
			string json = JsonConvert.SerializeObject(obj);

			// reutrn the boolean as a plain text string
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, JsonMediaTypeFormatter.DefaultMediaType.ToString())
			};
		}
	}
}