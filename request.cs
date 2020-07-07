using System;
using System.IO;

using System.Net;
using System.Net.Http;
using System.Web;

using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace request
{	
	class RequestBody 
	{
		private string requestBody = "";
		public string getBody
		{
			get { return requestBody; }
		}
		
		public void addParam(string key, dynamic val)
		{
			if (requestBody.Length == 0) 
			{
				requestBody += String.Format("{0}={1}", key, val);
			}
			else 
			{
				requestBody += String.Format("&{0}={1}", key, val);
			}
		}
	}
	class Request 
	{	
		private static HttpClient client = new HttpClient();
		private static Dictionary<string, dynamic> convertJSONToDict(string json)
		{
			Dictionary<string, dynamic> convertedData = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
			return convertedData;
		}
		
		
		public static async Task<Dictionary<string, dynamic>> getReq(string url)
		{
			HttpResponseMessage responseBody = await client.GetAsync(url);
			
			var jsonResult = responseBody.Content.ReadAsStringAsync().Result;
			Dictionary<string, dynamic> result = convertJSONToDict(jsonResult);
			
			return result;
		}
		
		public static async Task<Dictionary<string, dynamic>> postReq(string url, RequestBody bodyReq)
		{
			var content = new StringContent(bodyReq.getBody, Encoding.UTF8, "application/x-www-form-urlencoded");
			HttpResponseMessage responseBody = await client.PostAsync(url, content);
			
			var jsonResult = responseBody.Content.ReadAsStringAsync().Result;
			Dictionary<string, dynamic> result = convertJSONToDict(jsonResult);

			return result;
		}
 	}
}
