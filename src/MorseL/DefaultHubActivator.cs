﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace MorseL
{
    public class DefaultHubActivator<THub, TClient> : IHubActivator<THub, TClient>
        where THub : Hub<TClient>
    {
        private readonly IServiceProvider _serviceProvider;
        private bool? _created;
        internal bool _disposed;

        public DefaultHubActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public THub Create()
        {
            Debug.Assert(!_created.HasValue, "hub activators must not be reused.");

            _created = false;
            var hub = _serviceProvider.GetService<THub>();
            if (hub == null)
            {
                hub = ActivatorUtilities.CreateInstance<THub>(_serviceProvider);
                _created = true;
            }

            return hub;
        }

        public void Release(THub hub)
        {
            if (hub == null)
            {
                throw new ArgumentNullException(nameof(hub));
            }

            Debug.Assert(_created.HasValue, "hubs must be released with the hub activator they were created");

            _disposed = true;
            if (_created.Value)
            {
                hub.Dispose();
            }
        }
    }
}
