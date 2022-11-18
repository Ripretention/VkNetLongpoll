# VkNet Longpoll
VkNet Longpoll - This is flexible plugin VkNet that allows you to easily interact with VKontakte API Longpoll ⚡

##  Example usage
```c#
using VkNetLongpoll;
using VkNetLongpoll.Contexts;

var api = new VkApi();
api.Authorize(new ApiAuthParams { AccessToken = ACCESS_TOKEN });

var lp = new Longpoll(api, GROUP_ID);
lp.Handler.HearCommand("/ping", async ctx => 
{
    await ctx.ReplyAsync("pong");
});

lp.Start();
```
