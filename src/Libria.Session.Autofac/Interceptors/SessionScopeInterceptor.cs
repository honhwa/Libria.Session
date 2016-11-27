﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Libria.Session.Aspects;
using Libria.Session.Interfaces;

namespace Libria.Session.Autofac.Interceptors
{
	public class SessionScopeInterceptor : IInterceptor
	{
		private readonly ISessionScopeFactory _scopeFactory;

		public SessionScopeInterceptor(ISessionScopeFactory scopeFactory)
		{
			_scopeFactory = scopeFactory;
		}

		public void Intercept(IInvocation invocation)
		{
			var sessionScopeAttribute = invocation.MethodInvocationTarget
				.GetCustomAttributes(typeof (SessionScopeAttribute), true)
				.FirstOrDefault() as SessionScopeAttribute;

			if (sessionScopeAttribute != null)
			{
				var returnType = invocation.MethodInvocationTarget.ReturnType;
				if (returnType == typeof (Task) ||
				    returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof (Task<>))
				{
					var ctArgument = invocation.Arguments.FirstOrDefault(a => a is CancellationToken);

					var cancellationToken = (CancellationToken?)ctArgument ?? CancellationToken.None;

					var scope = _scopeFactory
						.Create(sessionScopeAttribute.ReadOnly, sessionScopeAttribute.Option,
							sessionScopeAttribute.IsolationLevel);

					invocation.Proceed();

					var task = (Task) invocation.ReturnValue;

					// Wait for the task to finish.
					task.Wait(cancellationToken);
					scope.SaveChanges();
					scope.Dispose();
				}
				else
				{
					using (var scope = _scopeFactory
						.Create(sessionScopeAttribute.ReadOnly, sessionScopeAttribute.Option,
							sessionScopeAttribute.IsolationLevel))
					{
						invocation.Proceed();
						scope.SaveChanges();
					}
				}
			}
			else
			{
				invocation.Proceed();
			}
		}
	}
}