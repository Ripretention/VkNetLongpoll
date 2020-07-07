using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

using Req = request.Request;
using RequestBody = request.RequestBody;
using UtilsMath = Utils.Math;

namespace vk
{	
	class Vk
	{	
		private VkConfig vkConfig;
		public VkApi vkApi;
		public VkLongPoll vkLongPoll;
		
		public Vk(string accessToken, string version, int groupId)
		{
			this.vkConfig = new VkConfig(accessToken, version, groupId);
			this.vkApi = new VkApi(this.vkConfig);
			this.vkLongPoll = new VkLongPoll(this.vkConfig);
			
			VkContextBody.SetConfig(this.vkConfig);
		}
		
	}
	class VkLongPoll
	{
		private VkConfig vkConfig;
		private List<Dictionary<string, dynamic>> commandList = new List<Dictionary<string, dynamic>>();
		private List<Dictionary<string, dynamic>> eventHandlerList = new List<Dictionary<string, dynamic>>();
		private Action<dynamic, dynamic> handlerMessageFunc;
		
		public VkLongPoll(VkConfig vkCfg)
		{
			this.vkConfig = vkCfg;
		}
		
		public void handlerMessage(Action<dynamic, dynamic> handlerMessageFunc)
		{
			this.handlerMessageFunc = handlerMessageFunc;
		}
		public void handlerEvent(string eventName, Action<dynamic> callback)
		{
			Dictionary<string, dynamic> eventBody = new Dictionary<string, dynamic>{ {eventName, callback} };
			this.eventHandlerList.Add(eventBody);
		}
		public void command(dynamic target, Action<dynamic, dynamic> callback) 
		{
			Dictionary<string, dynamic> commandBody = new Dictionary<string, dynamic>
			{
				{"target", target},
				{"callback", callback}
			};
			this.commandList.Add(commandBody);
		}
		
		private void handlingMessage(dynamic msgData)
		{
			bool dropedMsg = false;
			Action dropFunc = (() => { dropedMsg = true; });

			VkContextBody context = new VkContextBody(msgData["object"]);	
			if (this.handlerMessageFunc != null)
			{
				this.handlerMessageFunc(context, dropFunc);
			}
			
			if (context.isEvent && this.eventHandlerList.Count != 0 && !dropedMsg)
			{
				foreach (Dictionary<string, dynamic> element in this.eventHandlerList)
				{
					if (element.ContainsKey(context.eventType))
					{
						element[context.eventType](context);
						dropFunc();
					}
				}
			}
			
			if (dropedMsg) return;
			foreach (Dictionary<string, dynamic> element in this.commandList)
			{	
				bool isStringTarget = (element["target"] is string && context.text.Contains(element["target"]));
				bool isRegexTarget = (element["target"] is Regex && element["target"].IsMatch(context.text));
				
				bool isCommand = (isStringTarget || isRegexTarget);             
				if (isCommand && !dropedMsg)
				{
					ThreadPool.QueueUserWorkItem(state => element["callback"](context, dropFunc));
				}
			}
		}
		
		public async Task<string> getLongPollUrlConnection()
		{	
			VkApi vkApi = new VkApi(this.vkConfig);

			RequestBody reqBody = new RequestBody();
			var res = await vkApi.callMethod("groups.getLongPollServer", reqBody);
			
			return String.Format("{0}?act=a_check&key={1}&ts={2}&wait=25", res["server"], res["key"], res["ts"]);
		}
		
		public async Task startLongPoll()
		{
			if (!this.vkConfig.isValid()) throw new Exception("not valid vkCfg in LongPoll");
			
			string longPollUrlConn = await this.getLongPollUrlConnection();
			
			string lastTs = "";
			while (true)
			{
				var msgData = await Req.getReq(longPollUrlConn);
				
				if (msgData["ts"] == lastTs) continue;
				lastTs = msgData["ts"];
				
				this.handlingMessage(msgData["updates"].Last);
			}
		}
	}
	class VkApi
	{
		private const string methodsUrl = "https://api.vk.com/method";
		
		private VkConfig vkConfig;
		public VkApi(VkConfig vkCfg)
		{
			this.vkConfig = vkCfg;
		}
		
		public async Task<dynamic> callMethod(string nameMethod, RequestBody bodyMethod)
		{	
			if (!this.vkConfig.isValid()) throw new Exception("vkConfig is null");
				
			string url = String.Format("{0}/{1}", methodsUrl, nameMethod);
			
			bodyMethod.addParam("access_token", vkConfig.token);
			bodyMethod.addParam("v", vkConfig.v);
			bodyMethod.addParam("group_id", vkConfig.gId);
			
			Dictionary<string, dynamic> response = await Req.postReq(url, bodyMethod);
			if (response.ContainsKey("error"))
			{	
				Console.WriteLine(Convert.ToString(response["error"]));
			}
			
			if (!response.ContainsKey("response")) return response;
			
			return response["response"];
		}
	}
	class VkConfig
	{
		private string accessToken = "";
		private string version = "";
		private int groupId = 0;
		
		public string token
		{
			get { return this.accessToken; }
		}
		public string v
		{
			get { return this.version; }
		}
		public int gId
		{
			get { return this.groupId; }
		}
		public bool isValid()
		{
			return (this.accessToken.Length > 0 && this.version.Length > 0 && this.groupId > 0);
		}
		
		public VkConfig(string accessToken, string version, int groupId)
		{
			if (accessToken == null) throw new Exception("vkConfig is null");
			
			this.accessToken = accessToken;
			this.version = version;
			this.groupId = groupId;
		}
	}
	class VkContextBody
	{	
		static VkConfig vkConfig;
		
		public string text;
		public int date;
		public int peerId;
		public int senderId;
		public int senderType;
		public int chatId;
		
		public string eventType;
		public int eventMemberId;
		
		static public Dictionary<string, dynamic> properties = new Dictionary<string, dynamic>();
        public dynamic this[string name] 
        {
            get 
            {
                if (properties.ContainsKey(name)) 
                {
                    return properties[name];
                }
                return null;
            }
            set 
            {
				Console.WriteLine(value);
                properties[name] = value;
            }
        }
		
		public VkContextBody(JObject msgData)
		{
			this.text = (string)msgData["text"];
			this.date = (int)msgData["date"];
			this.peerId = (int)msgData["peer_id"];
			this.senderId = (int)msgData["from_id"];
			this.chatId = this.convertPeerIdToChatId(this.peerId);
			this.senderType = (this.senderId > 0) ? 1 : 0;
			
			if (msgData["action"] != null)
			{
				this.eventType = (string)msgData["action"]["type"];
				this.eventMemberId = (int)msgData["action"]["member_id"];
			}
		}
		
		public async Task msgSend(string msgText) 
		{
			VkApi vkApi = new VkApi(vkConfig);
			
			RequestBody reqBody = new RequestBody();
			reqBody.addParam("message", msgText);
			reqBody.addParam("random_id", UtilsMath.getRandomInt());
			reqBody.addParam("peer_id", this.peerId);
			
			await vkApi.callMethod("messages.send", reqBody);
		}
		
		public int convertPeerIdToChatId(int peerId)
		{
			MatchCollection matches = Regex.Matches(Convert.ToString(peerId), @"20+(\d+)");
			
			int chatId = Convert.ToInt16(matches[0].Groups[1].Value);
			return chatId;
		}
		
		static public void SetConfig(VkConfig vkCfg)
		{
			vkConfig = vkCfg;
		}
		
		public bool isEvent
		{
			get { return (this.eventType != null); }
		}
	}
}
