using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Swagger.ObjectModel;
using Swagger.ObjectModel.Builders;

namespace Nancy.Swagger.Services
{
    [SwaggerApi]
    public abstract class SwaggerMetadataProvider : ISwaggerMetadataProvider
    {
        private static Info _info = new Info()
        {
            Title = "No title set",
            Version = "0.1",
            Description = ""
        };

        private static SwaggerRoot _swaggerRoot = null;

        private static IDictionary<string, SecuritySchemeBuilder> _securitySchemes = new Dictionary<string, SecuritySchemeBuilder>();

        private static IDictionary<SecurityScheme, SecurityRequirementBuilder> _securityRequirements = new Dictionary<SecurityScheme, SecurityRequirementBuilder>();

        public static void SetInfo(string title, string version, string desc, Contact contact = null, string termsOfService = null)
        {
            _info = new Info()
            {
                Title = title,
                Version = version,
                Description = desc,
                Contact = contact,
                TermsOfService = termsOfService
            };
        }

        public static void AddSecuritySchemeBuilder(SecuritySchemeBuilder builder, string name)
        {
            if (_securitySchemes == null)
            {
                _securitySchemes = new Dictionary<string, SecuritySchemeBuilder>();
            }

            if (_securitySchemes.ContainsKey(name))
            {
                _securitySchemes.Remove(name);
            }

            _securitySchemes.Add(name, builder);
        }

        public static void AddSecurityRequirementBuilder(SecurityScheme securityScheme, SecurityRequirementBuilder builder)
        {
            if (_securityRequirements == null)
            {
                _securityRequirements = new Dictionary<SecurityScheme, SecurityRequirementBuilder>();
            }

            if (_securityRequirements.ContainsKey(securityScheme))
            {
                _securityRequirements.Remove(securityScheme);
            }

            _securityRequirements.Add(securityScheme, builder);
        }

        public static void SetSecuritySchemeBuilder(SecuritySchemeBuilder builder, string name)
        {
            _securitySchemes = null;
            AddSecuritySchemeBuilder(builder, name);
        }

        public static void SetSecurityRequirementBuilder(SecurityScheme securityScheme, SecurityRequirementBuilder builder)
        {
            _securityRequirements = null;
            AddSecurityRequirementBuilder(securityScheme, builder);
        }

        /// <summary>
        /// Allows other root level swagger values to be set.
        /// </summary>
        public static void SetSwaggerRoot(string host = null, string basePath = null, IEnumerable<Schemes> schemes = null, 
            IEnumerable<string> consumes = null, IEnumerable<string> produces = null, ExternalDocumentation externalDocumentation = null)
        {
            _swaggerRoot = new SwaggerRoot
            {
                Host = host,
                BasePath = basePath,
                Schemes = schemes,
                Consumes = consumes,
                Produces = produces,
                ExternalDocumentation = externalDocumentation
            };
        }

        public SwaggerRoot GetSwaggerJson(NancyContext context)
        {
            var builder = new SwaggerRootBuilder();

            if (_swaggerRoot?.Host != null)
            {
                builder.Host(_swaggerRoot.Host);
            }

            if (_swaggerRoot?.BasePath != null)
            {
                builder.BasePath(_swaggerRoot.BasePath);
            }

            if (_swaggerRoot?.Schemes != null)
            {
                _swaggerRoot.Schemes.ToList().ForEach(x => builder.Scheme(x));
            }

            if (_swaggerRoot?.Consumes != null)
            {
                builder.ConsumeMimeTypes(_swaggerRoot.Consumes);
            }

            if (_swaggerRoot?.Produces != null)
            {
                builder.ProduceMimeTypes(_swaggerRoot.Produces);
            }

            if (_swaggerRoot?.ExternalDocumentation != null)
            {
                builder.ExternalDocumentation(_swaggerRoot.ExternalDocumentation);
            }

            foreach (var pathItem in this.RetrieveSwaggerPaths(context))
            {
                builder.Path(pathItem.Key, pathItem.Value.PathItem);
            }

            builder.Info(_info);
            
            foreach (var model in RetrieveSwaggerModels())
            {
                // arrays do not have to be defined in definitions, they are already being declared fully inline
                // either they should use #ref or they shouldn't be in definitions
                if (model.ModelType.IsContainer())
                    continue;

                builder.Definition(SwaggerConfig.ModelIdConvention(model.ModelType), model.GetSchema(true));
            }

            foreach (var tag in RetrieveSwaggerTags().OrderBy(x => x.Name))
            {
                builder.Tag(tag);
            }


            foreach (var securityScheme in _securitySchemes)
            {
                builder.SecurityDefinition(securityScheme.Key, securityScheme.Value.Build());
            }

            foreach (var securityRequirement in _securityRequirements)
            {
                builder.SecurityRequirement(securityRequirement.Value);
            }

            return builder.Build();
        }

        protected abstract IDictionary<string, SwaggerRouteData> RetrieveSwaggerPaths(NancyContext context);

        protected abstract IList<SwaggerModelData> RetrieveSwaggerModels();

        protected abstract IList<Tag> RetrieveSwaggerTags();

        private static Type GetType(Type type)
        {
            if (type.IsContainer())
            {
                return type.GetElementType() ?? type.GetTypeInfo().GetGenericArguments().First();
            }

            return type;
        }

        private SwaggerModelData EnsureModelData(Type type, IList<SwaggerModelData> modelData)
        {
            return modelData.FirstOrDefault(x => x.ModelType == type) ?? new SwaggerModelData(type);
        }
    }
}