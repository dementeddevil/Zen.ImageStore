using System.Linq;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Swashbuckle.Swagger.Model;
using Swashbuckle.SwaggerGen.Generator;

namespace Zen.ImageStore.Site
{
    public class RemoveCancellationTokenOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            context.ApiDescription.ParameterDescriptions
                .Where(pd =>
                    pd.ModelMetadata?.ContainerType == typeof(CancellationToken) ||
                    pd.ModelMetadata?.ContainerType == typeof(WaitHandle) ||
                    pd.ModelMetadata?.ContainerType == typeof(SafeWaitHandle))
                .ToList()
                .ForEach(
                    pd =>
                    {
                        if (operation.Parameters != null)
                        {
                            var cancellationTokenParameter = operation.Parameters.Single(p => p.Name == pd.Name);
                            operation.Parameters.Remove(cancellationTokenParameter);
                        }
                    });
        }
    }
}