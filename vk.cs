using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using Req = request.Request;
using RequestBody = request.RequestBody;
using UtilsMath = Utils.Math;

namespace vk
{	
	class Vk
	{	
		private VkConfig config;
		public VkApi Api;
		public VkLongPoll LongPoll;
		
		public Vk(string accessToken, string version, int groupId)
		{
			config = new VkConfig(accessToken, version, groupId);
			Api = new VkApi(config);
			LongPoll = new VkLongPoll(config);
			
			VkContextBody.SetConfig(config);
		}
	}
	
	class VkLongPoll
	{
		private VkConfig config;
		private List<Dictionary<string, dynamic>> commandList = new List<Dictionary<string, dynamic>>();
		private List<Dictionary<string, dynamic>> eventHandlerList = new List<Dictionary<string, dynamic>>();
		private Action<dynamic, dynamic> handlerMessageFunc;
		
		public VkLongPoll(VkConfig vkCfg)
		{
			config = vkCfg;
		}
		
		public void handlerMessage(Action<dynamic, dynamic> handlerMessageFunc)
		{
			this.handlerMessageFunc = handlerMessageFunc;
		}
		public void handlerEvent(string eventName, Action<dynamic> callback)
		{
			Dictionary<string, dynamic> eventBody = new Dictionary<string, dynamic>{ {eventName, callback} };
			eventHandlerList.Add(eventBody);
		}
		public void command(dynamic target, Action<dynamic, dynamic> callback) 
		{
			Dictionary<string, dynamic> commandBody = new Dictionary<string, dynamic>
			{
				{"target", target},
				{"callback", callback}
			};
			commandList.Add(commandBody);
		}
		
		private void handlingMessage(dynamic msgData)
		{
			bool dropedMsg = false;
			Action dropFunc = (() => { dropedMsg = true; });
			
			VkContextBody context = new VkContextBody(msgData["object"]["message"]);
			if (handlerMessageFunc != null) handlerMessageFunc(context, dropFunc);
			
			if (context.IsEvent && eventHandlerList.Count != 0 && !dropedMsg)
			{
				foreach (Dictionary<string, dynamic> element in eventHandlerList)
				{
					if (element.ContainsKey(context.EventType))
					{
						element[context.EventType](context);
						dropFunc();
					}
				}
			}
			
			if (dropedMsg) return;
			foreach (Dictionary<string, dynamic> element in commandList)
			{	
				bool isStringTarget = (element["target"] is string && context.Text.Contains(element["target"]));
				bool isRegexTarget = (element["target"] is Regex && element["target"].IsMatch(context.Text));
				
				bool isCommand = (isStringTarget || isRegexTarget);
				if (isCommand && !dropedMsg)
				{
					if (isRegexTarget) 
					{
						context.Match = element["target"].Match(context.Text);
					}
					
					ThreadPool.QueueUserWorkItem(state => element["callback"](context, dropFunc));
				}
			}
		}
		
		public async Task<string> getLongPollUrlConnection()
		{	
			VkApi vkApi = new VkApi(config);
			
			var res = await vkApi.callMethod("groups.getLongPollServer", new RequestBody());
			return String.Format("{0}?act=a_check&key={1}&ts={2}&wait=25", res["server"], res["key"], res["ts"]);
		}
		
		public async Task startLongPoll()
		{
			if (!config.Valid()) throw new Exception("not valid vkCfg in LongPoll");
			
			string longPollUrlConn = await this.getLongPollUrlConnection();
			
			string lastTs = "";
			while (true)
			{
				var msgData = await Req.GetReq(longPollUrlConn);
				
				if (msgData["ts"] == lastTs) continue;
				lastTs = msgData["ts"];
				
				this.handlingMessage(msgData["updates"].Last);
			}
		}
	}
	class VkApi
	{
		private const string methodsUrl = "https://api.vk.com/method";
		private VkConfig config;

		public VkApi(VkConfig vkCfg)
		{
			config = vkCfg;
		}
		
		public async Task<dynamic> callMethod(string nameMethod, RequestBody bodyMethod)
		{	
			if (!config.Valid()) throw new Exception("vkConfig is null");
				
			string url = String.Format("{0}/{1}", methodsUrl, nameMethod);
			
			bodyMethod.AddParam("access_token", config.AccessToken);
			bodyMethod.AddParam("v", config.Version);
			bodyMethod.AddParam("group_id", config.GroupId);

			Dictionary<string, dynamic> response = await Req.PostReq(url, bodyMethod);
			if (response.ContainsKey("error")) throw new Exception(String.Format("Code: {0} \n In method: {1} \n Message: {2}", response["error"]["error_code"], nameMethod, response["error"]["error_msg"]));
			
			if (!response.ContainsKey("response")) return response;
			return response["response"];
		}
	}
	
	class VkConfig
	{
		public string AccessToken { get; private set; }
		public string Version { get; private set; }
		public int GroupId { get; private set; }
		
		public bool Valid()
		{
			return (AccessToken != null && Version != null && GroupId != null);
		}
		
		public VkConfig(string accessToken, string version, int groupId)
		{
			AccessToken = accessToken;
			Version = version;
			GroupId = groupId;
		}
	}
	
	class VkContextBody
	{	
		static VkConfig vkConfig;
		
		public string Text;
		public int Date;
		public int PeerId;
		public int SenderId;
		public int SenderType;
		public int ChatId;
		
		public string EventType;
		public int EventMemberId;
		public Match Match;
		
		public bool IsEvent { get { return (EventType != null); } }
		
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
                properties[name] = value;
            }
        }
		
		public VkContextBody(JObject msgData)
		{
			Text = (string)msgData["text"];
			Date = (int)msgData["date"];
			PeerId = (int)msgData["peer_id"];
			SenderId = (int)msgData["from_id"];
			
			ChatId = convertPeerIdToChatId(PeerId);
			SenderType = (SenderId > 0) ? 1 : 0;
			
			if (msgData["action"] != null)
			{
				EventType = (string)msgData["action"]["type"];
				EventMemberId = (int)msgData["action"]["member_id"];
			}
		}
		
		public async Task SendMsg(string msgText) 
		{
			VkApi vkApi = new VkApi(vkConfig);
			
			RequestBody reqBody = new RequestBody();
			reqBody.AddParam("message", msgText);
			reqBody.AddParam("random_id", UtilsMath.getRandomInt());
			reqBody.AddParam("peer_id", PeerId);
			
			await vkApi.callMethod("messages.send", reqBody);
		}
		
		private int convertPeerIdToChatId(int peerId)
		{
			MatchCollection matches = Regex.Matches(Convert.ToString(peerId), @"20+(\d+)");
			
			int chatId = Convert.ToInt16(matches[0].Groups[1].Value);
			return chatId;
		}
		
		static public void SetConfig(VkConfig vkCfg)
		{
			vkConfig = vkCfg;
		}
	}
}
