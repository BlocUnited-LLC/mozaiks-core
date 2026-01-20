using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthServer.Api.Filters
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null) return;

            var formFileParameters = context.MethodInfo.GetParameters()
                .Where(p => p.GetCustomAttributes(typeof(FromFormAttribute), true).Any() && p.ParameterType == typeof(IFormFile))
                .ToList();

            foreach (var parameter in formFileParameters)
            {
                var parameterName = parameter.Name;
                var schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                };

                var formFileParameter = new OpenApiParameter
                {
                    Name = parameterName,
                    In = ParameterLocation.Query,
                    Required = true,
                    Schema = schema
                };

                operation.Parameters.Add(formFileParameter);
            }
        }
    }
}
