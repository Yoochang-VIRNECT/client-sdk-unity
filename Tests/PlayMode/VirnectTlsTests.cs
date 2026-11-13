using System;
using System.Collections;
using LiveKit.PlayModeTests.Utils;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LiveKit.PlayModeTests
{
    public class VirnectTlsTests
    {
        // Run in separate Editor processes: the native TLS policy is initialized once.
        // Requires a self-signed TLS proxy in front of the local LiveKit dev server.
        [UnityTest, Category("E2E")]
        public IEnumerator SelfSignedServer_RespectsTlsPolicy()
        {
            var url = Environment.GetEnvironmentVariable("LK_TEST_SELF_SIGNED_URL");
            if (string.IsNullOrEmpty(url))
                Assert.Ignore("Set LK_TEST_SELF_SIGNED_URL to a self-signed local LiveKit endpoint.");
            StringAssert.StartsWith("wss://", url);
            var policy = Environment.GetEnvironmentVariable("LIVEKIT_SKIP_CERT_VERIFY");
            Assert.That(policy, Is.EqualTo("true").Or.EqualTo("false"),
                "Explicitly select true or false in a fresh Editor process.");
            var options = TestRoomContext.ConnectionOptions.Default;
            options.ServerUrl = url;
            var previous = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                using var context = new TestRoomContext(options);
                yield return context.ConnectAll();
                if (policy == "true")
                {
                    Assert.IsNull(context.ConnectionError, context.ConnectionError);
                    Assert.IsTrue(context.Rooms[0].IsConnected);
                }
                else
                {
                    Assert.IsNotNull(context.ConnectionError,
                        "Strict TLS must reject the self-signed certificate.");
                    Assert.IsFalse(context.Rooms[0].IsConnected);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }
        }
    }
}
