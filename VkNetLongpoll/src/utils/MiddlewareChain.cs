using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VkNetLongpoll
{
    public class MiddlewareChain<T>
    {
        private List<Middleware<T>> middlewares = new List<Middleware<T>>();

        /// <summary>
        /// Adds middleware to the chain
        /// </summary>
        public MiddlewareChain<T> Use(Middleware<T> middleware) 
        {
            middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// The number of middleware installed in MiddlewareChain
        /// </summary>
        public int Count { get => middlewares.Count; }

        /// <summary>
        /// Run middlewares chain
        /// </summary>
        public void Execute(T context)
        {
            foreach (var middleware in middlewares)
                if (!middleware.Call(context))
                    break;
        }
        public async Task ExecuteAsync(T context)
        {
            foreach (var middleware in middlewares)
                if (!await middleware.CallAsync(context))
                    break;
        }
    }

    public class Middleware<T>
    {
        private bool nextCalled = false;
        private readonly Func<T, Action, dynamic> fn;
        public Middleware(Func<T, Action, dynamic> fn)
        {
            this.fn = fn;
        }
        public Middleware(Action<T, Action> fn)
        {
            this.fn = (ctx, next) =>
            {
                fn(ctx, next);
                return null;
            };
        }

        public bool Call(T context)
        {
            fn(context, Next);
            return nextCalled;
        }
        public async Task<bool> CallAsync(T context)
        {
            await fn(context, Next);
            return nextCalled;
        }

        public void Next()
        {
            nextCalled = true;
        }
    }
}
