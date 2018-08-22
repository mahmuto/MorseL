﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MorseL.Common;
using MorseL.Common.Serialization;
using MorseL.Extensions;
using MorseL.Shared.Tests;
using MorseL.Sockets;
using Xunit;
using Connection = MorseL.Sockets.Connection;

namespace MorseL.Tests
{
    [Trait("Target", "Hubs")]
    public class HubTests
    {
        private int _nextId;

        [Fact]
        public async void HubActivatorReleasedWhenExceptionThrownInOnConnectedAsync()
        {
            var serviceProvider = CreateServiceProvider(s => s.AddSingleton(typeof(IHubActivator<,>), typeof(DefaultHubActivator<,>)));
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<BadHub>>();
            var hubActivator = serviceProvider.GetRequiredService<IHubActivator<BadHub, IClientInvoker>> ();
            var webSocket = new LinkedFakeSocket();

            await Assert.ThrowsAnyAsync<Exception>(() => actualHub.OnConnected(webSocket, new DefaultHttpContext()));

            Assert.True(((DefaultHubActivator<BadHub, IClientInvoker>)hubActivator)._disposed);
        }

        [Fact]
        public async void CanCallVoidMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.VoidMethod), null);

            var result = await ReadInvocationResultFromSocket<object>(webSocket);

            Assert.NotNull(result);
            Assert.Null(result.Result);
        }

        [Fact]
        public async void CanCallReturnVoidAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.VoidMethodAsync), null);

            var result = await ReadInvocationResultFromSocket<object>(webSocket);

            Assert.NotNull(result);
            Assert.Null(result.Result);
        }

        [Fact]
        public async void CanCallReturnIntMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.IntMethod), null);

            var result = await ReadInvocationResultFromSocket<int>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.IntResult);
        }

        [Fact]
        public async void CanCallReturnIntAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.IntMethodAsync), null);

            var result = await ReadInvocationResultFromSocket<int>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.IntResult);
        }

        [Fact]
        public async void CanCallReturnStringMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.StringMethod), null);

            var result = await ReadInvocationResultFromSocket<string>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.StringResult);
        }

        [Fact]
        public async void CanCallReturnStringAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.StringMethodAsync), null);

            var result = await ReadInvocationResultFromSocket<string>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.StringResult);
        }

        [Fact]
        public async void CanCallReturnFloatMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.FloatMethod), null);

            var result = await ReadInvocationResultFromSocket<float>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.FloatResult);
        }

        [Fact]
        public async void CanCallReturnFloatAsyncMethodOnHub()
        {
            var serviceProvider = CreateServiceProvider();
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            await SendMessageToSocketAsync(actualHub, connection, nameof(TestHub.FloatMethod), null);

            var result = await ReadInvocationResultFromSocket<float>(webSocket);

            Assert.NotNull(result);
            Assert.Equal(result.Result, TestHub.FloatResult);
        }

        [Theory]
        [InlineData("methodName1")]
        [InlineData("methodName2", "some argument")]
        [InlineData("methodName3", "some argument", 5)]
        public async void CannotCallNonExistentMethod(string methodName, params object[] arguments)
        {
            var serviceProvider = CreateServiceProvider(s => s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnMissingHubMethodRequest = true));
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
            var expectedArgumentList = arguments?.Length > 0 ? String.Join(", ", arguments) : "[No Parameters]";

            var exception = await Assert.ThrowsAsync<MorseLException>(() => SendMessageToSocketAsync(actualHub, connection, methodName, arguments));
            Assert.Equal($"Invalid method request received from {connection.Id}; method is \"{expectedMethodName}({expectedArgumentList})\"", exception.Message);
        }

        [Theory]
        [InlineData("", "some argument", 5)]
        [InlineData(null, "some other argument", 42)]
        public async void CannotCallInvalidMethodName(string methodName, params object[] arguments)
        {
            var serviceProvider = CreateServiceProvider(s => s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnMissingHubMethodRequest = true));
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            var expectedMethodName = string.IsNullOrWhiteSpace(methodName) ? "[Invalid Method Name]" : methodName;
            var expectedArgumentList = arguments?.Length > 0 ? String.Join(", ", arguments) : "[No Parameters]";

            var exception = await Assert.ThrowsAsync<MorseLException>(() => SendMessageToSocketAsync(actualHub, connection, methodName, arguments));
            Assert.Equal($"Invalid method request received from {connection.Id}; method is \"{expectedMethodName}({expectedArgumentList})\"", exception.Message);
        }

        [Theory]
        [InlineData("invalid message 1")]   // Invalid JSON
        [InlineData("{}")]                  // Valid JSON, invalid message
                                            // (Note: valid message will work and return invocation error result)
        public async void CannotCallInvalidInvocationRequest(string message)
        {
            var serviceProvider = CreateServiceProvider(s => s.Configure<Extensions.MorseLOptions>(o => o.ThrowOnInvalidMessage = true));
            var actualHub = serviceProvider.GetRequiredService<HubWebSocketHandler<TestHub>>();
            var webSocket = new LinkedFakeSocket();

            var connection = await CreateHubConnectionFromSocket(actualHub, webSocket);

            var exception = await Assert.ThrowsAsync<MorseLException>(() => SendMessageToSocketAsync(actualHub, connection, message));
            Assert.Equal($"Invalid message received \"{message}\" from {connection.Id}", exception.Message);
        }

        private async Task<Connection> CreateHubConnectionFromSocket<THub>(HubWebSocketHandler<THub> actualHub, LinkedFakeSocket webSocket)
            where THub : Hub
        {
            var connection = await actualHub.OnConnected(webSocket, new DefaultHttpContext());

            // Receive the connection message
            var connectMessage = await ReadMessageFromSocketAsync(webSocket);

            Assert.NotNull(connectMessage);
            Assert.NotNull(connectMessage.Data);
            Assert.NotNull(Guid.Parse(connectMessage.Data));
            return connection;
        }

        private async Task SendMessageToSocketAsync(WebSocketHandler handler, Connection connection, string methodName, params object[] args)
        {
            var serializedMessage = Json.SerializeObject(new InvocationDescriptor()
            {
                Id = Interlocked.Increment(ref _nextId).ToString(),
                MethodName = methodName,
                Arguments = args
            });
            await handler.ReceiveAsync(connection, serializedMessage);
        }

        private async Task SendMessageToSocketAsync(WebSocketHandler handler, Connection connection, string message)
        {
            await handler.ReceiveAsync(connection, message);
        }

        private async Task<Message> ReadMessageFromSocketAsync(WebSocket socket)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024 * 4]);
            string serializedMessage;

            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    serializedMessage = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            return Json.Deserialize<Message>(serializedMessage);
        }

        private async Task<InvocationResultDescriptor> ReadInvocationResultFromSocket<TReturnType>(WebSocket socket)
        {
            var message = await ReadMessageFromSocketAsync(socket);
            var pendingCalls = new Dictionary<string, InvocationRequest>();
            pendingCalls.Add(_nextId.ToString(), new InvocationRequest(new CancellationToken(), typeof(TReturnType)));
            return Json.DeserializeInvocationResultDescriptor(message.Data, pendingCalls);
        }

        private IServiceProvider CreateServiceProvider(Action<ServiceCollection> addServices = null)
        {
            var services = new ServiceCollection();
            services.AddOptions()
                .AddLogging()
                .AddMorseL();

            addServices?.Invoke(services);

            return services.BuildServiceProvider();
        }

        public class BadHub : Hub
        {
            public override Task OnConnectedAsync(Connection connection)
            {
                throw new Exception();
            }
        }

        public class TestHub : Hub
        {
            public const int IntResult = 42;
            public const string StringResult = "42";
            public const float FloatResult = 42.42f;

            public void VoidMethod() { }
            public Task VoidMethodAsync() { return Task.CompletedTask; }

            public int IntMethod() { return IntResult; }
            public Task<int> IntMethodAsync() { return Task.FromResult(IntResult); }

            public string StringMethod() { return StringResult; }
            public Task<string> StringMethodAsync() { return Task.FromResult(StringResult); }

            public float FloatMethod() { return FloatResult; }
            public Task<float> FloatMethodAsync() { return Task.FromResult(FloatResult); }
        }
    }
}
