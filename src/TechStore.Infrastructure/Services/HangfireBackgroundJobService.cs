using System.Linq.Expressions;
using Hangfire;
using TechStore.Application.Common.Interfaces;

namespace TechStore.Infrastructure.Services
{
    public class HangfireBackgroundJobService : IBackgroundJobService
    {
        public string Enqueue(Expression<Action> methodCall)
        {
            return BackgroundJob.Enqueue(methodCall);
        }
    }
}
