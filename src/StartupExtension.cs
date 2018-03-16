﻿using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NToastNotify.Components;
using NToastNotify.Libraries;
using NToastNotify.Libraries.Toastr;
using NToastNotify.MessageContainers;

namespace NToastNotify
{
    public static class StartupExtension
    {
        private static EmbeddedFileProvider _embeddedFileProvider;
        private const string NToastNotifyCorsPolicy = nameof(NToastNotifyCorsPolicy);
        private static readonly Assembly ThisAssembly = typeof(ToastrViewComponent).Assembly;
        private static EmbeddedFileProvider GetEmbeddedFileProvider()
        {
            return _embeddedFileProvider ??
          (_embeddedFileProvider = new EmbeddedFileProvider(ThisAssembly, "NToastNotify"));
        }
        [Obsolete("Please use the extension method to IMVCBuilder. For e.g. services.AddMvc().AddNToastNotify()", true)]
        public static IServiceCollection AddNToastNotify(this IServiceCollection services, ToastrOptions defaultOptions = null, NToastNotifyOption nToastNotifyOptions = null, IMvcBuilder mvcBuilder = null)
        {
            return services;
        }

        /// <summary>
        /// Addes the necessary services for NToastNotify. Default <see cref="ILibrary{TOption}" used is <see cref="Toastr"/>/>
        /// </summary>
        /// <typeparam name="TLibrary">Toastr</typeparam>
        /// <param name="mvcBuilder"></param>
        /// <param name="defaultOptions"></param>
        /// <param name="nToastNotifyOptions"></param>
        /// <returns></returns>
        [Obsolete("Please use the library specific method either AddNToastNotifyToastr or AddNToastNotifyNoty")]
        public static IMvcBuilder AddNToastNotify(this IMvcBuilder mvcBuilder, ToastrOptions defaultOptions = null,
    NToastNotifyOption nToastNotifyOptions = null)
        {
            return AddNToastNotifyToMvcBuilder<ToastrLibrary, ToastrOptions, ToastrMessage, ToastrNotification>(mvcBuilder, defaultOptions, nToastNotifyOptions);
        }

        /// <summary>
        /// Add the NToastNotify middleware to handle ajax request.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseNToastNotify(this IApplicationBuilder builder)
        {
            builder.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = GetEmbeddedFileProvider(),
                RequestPath = new PathString("/ntoastnotify")
            });
            builder.UseCors(NToastNotifyCorsPolicy);
            builder.UseMiddleware<NtoastNotifyMiddleware>();
            return builder;
        }

        public static IMvcBuilder AddNToastNotifyToMvcBuilder<TLibrary, TOption, TMessage, TNotificationImplementation>(this IMvcBuilder mvcBuilder, TOption defaultLibOptions,
            NToastNotifyOption nToastNotifyOptions = null)
            where TLibrary: class, ILibrary<TOption>, new()
            where TOption: class, ILibraryOptions
            where TMessage: class, IToastMessage
            where TNotificationImplementation : class, IToastNotification
        {
            var services = mvcBuilder.Services;
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddCors(options =>
            {
                options.AddPolicy(NToastNotifyCorsPolicy,
                    builder =>
                    {
                        builder.WithExposedHeaders(Constants.RequestHeaderKey, Constants.ResponseHeaderKey);
                    });
            });

            //Add the file provider to the Razor view engine
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Add(GetEmbeddedFileProvider());
            });

            //This is a fix for Feature folders based project structure. Add the view location to ViewLocationExpanders.
            mvcBuilder?.AddRazorOptions(o =>
            {
                o.ViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
            });

            //Check if a TempDataProvider is already registered.
            var tempDataProvider = services.FirstOrDefault(d => d.ServiceType == typeof(ITempDataProvider));
            if (tempDataProvider == null)
            {
                //Add a tempdata provider when one is not already registered
                services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            }

            //Add TempDataWrapper for accessing and adding values to tempdata.
            services.AddSingleton<ITempDataWrapper, TempDataWrapper>();

            //check if IHttpContextAccessor implementation is not registered. Add one if not.
            var httpContextAccessor = services.FirstOrDefault(d => d.ServiceType == typeof(IHttpContextAccessor));
            if (httpContextAccessor == null)
            {
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            }

            var library = new TLibrary();

            // Add the toast library default options that will be rendered by the viewcomponent
            services.AddSingleton<ILibraryOptions>(defaultLibOptions ?? library.Defaults);
            services.AddSingleton<ILibrary<TOption>, TLibrary>();

            // Add the NToastifyOptions to the services container for DI retrieval 
            //(options that are not rendered as they are not part of the plugin)
            nToastNotifyOptions = nToastNotifyOptions ?? new NToastNotifyOption();
            nToastNotifyOptions.LibraryDetails = library;
            services.AddSingleton(nToastNotifyOptions);
            services.AddSingleton<IMessageContainerFactory, MessageContainerFactory>();
            services.AddScoped(typeof(IToastMessagesAccessor<IToastMessage>), typeof(ToastMessagesAccessor<TMessage>));
            //Add the ToastNotification implementation
            services.AddSingleton<IToastNotification, TNotificationImplementation>();
            services.AddScoped<NtoastNotifyMiddleware>();
            return mvcBuilder;
        }
    }
}