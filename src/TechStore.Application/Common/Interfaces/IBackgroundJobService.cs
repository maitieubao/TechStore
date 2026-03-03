using System.Linq.Expressions;

namespace TechStore.Application.Common.Interfaces
{
    public interface IBackgroundJobService
    {
        string Enqueue(Expression<Action> methodCall);
    }
}
