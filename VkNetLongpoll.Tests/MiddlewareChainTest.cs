using VkNetLongpoll.Utils;
namespace VkNetLongpoll.Tests;
public class MiddlewareChainTest
{
    [Test]
    public void RejectTest()
    {
        var tm = new TestMessage()
        {
            Text = "/adm help",
            User = new TestUser()
            {
                IsAdmin = false
            }
        };

        var result = runTestMiddlwareChain(tm);

        Assert.AreEqual(result.Rejected, 1);
        Assert.AreEqual(result.Handled, 2);
    }
    [Test]
    public void WorkTest()
    {
        var tm = new TestMessage()
        {
            Text = "just message",
            User = new TestUser()
            {
                IsAdmin = true
            }
        };

        var result = runTestMiddlwareChain(tm);

        Assert.AreEqual(result.Handled, 2);
        Assert.AreEqual(result.Rejected, 0);
    }
    [Test]
    public void WorkTest2()
    {
        var tm = new TestMessage()
        {
            Text = "/foo",
            User = new TestUser()
            {
                IsAdmin = true
            }
        };

        var result = runTestMiddlwareChain(tm);

        Assert.AreEqual(result.Handled, 3);
        Assert.AreEqual(result.Rejected, 0);
    }
    
    private MiddlewareChainResult runTestMiddlwareChain(TestMessage tm)
    {
        var middleware = new MiddlewareChain<TestMessage>();
        var result = new MiddlewareChainResult();

        middleware.Use(new Middleware<TestMessage>((ctx, next) =>
        {
            result.Handled++;
            next();
        }));
        middleware.Use(new Middleware<TestMessage>((ctx, next) =>
        {
            result.Handled++;
            if (ctx.Text.StartsWith("/"))
                next();
        }));
        middleware.Use(new Middleware<TestMessage>((ctx, next) =>
        {
            if (ctx.Text.StartsWith("/adm") && !ctx.User.IsAdmin)
                result.Rejected++;
            else
                result.Handled++;
        }));

        middleware.Execute(tm);
        return result;
    }
}

class TestMessage
{
    public string Text;
    public TestUser User;
}
class TestUser
{
    public uint Id = 1;
    public bool IsAdmin;
}
class MiddlewareChainResult
{
    public int Rejected = 0;
    public int Handled = 0;
}