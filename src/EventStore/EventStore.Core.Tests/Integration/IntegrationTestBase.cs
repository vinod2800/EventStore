﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Common.Settings;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration
{
    public class IntegrationTestBase
    {
        private TFChunkDb _db;

        private SingleVNode _vNode;

        [SetUp]
        protected virtual void SetUp()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "EventStoreTests", Guid.NewGuid().ToString());

            Directory.CreateDirectory(dbPath);

            var chunkSize = 256*1024*1024;
            var chunksToCache = 2;

            ICheckpoint writerChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"),
                                                                   Checkpoint.Writer, cached: true);
            ICheckpoint chaserChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"),
                                                                   Checkpoint.Chaser, cached: true);
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksToCache,
                                                 writerChk,
                                                 new[] {chaserChk});

            var settings = new SingleVNodeSettings(new IPEndPoint(IPAddress.Loopback, 1111),
                                                   new IPEndPoint(IPAddress.Loopback, 2111),
                                                   new[] {new IPEndPoint(IPAddress.Loopback, 2111).ToHttpUrl()});
            var appsets = new SingleVNodeAppSettings(TimeSpan.FromDays(1));
            _db = new TFChunkDb(nodeConfig);

            _vNode = new SingleVNode(_db, settings, appsets);
            

            var startCallback = new EnvelopeCallback<SystemMessage.SystemStart>();
            _vNode.Bus.Subscribe<SystemMessage.SystemStart>(startCallback);
            
            _vNode.Start();
            startCallback.Wait();
        }

        [TearDown]
        protected virtual void TearDown()
        {
            try
            {
                _vNode.Stop();
                _db.Dispose();
                Directory.Delete(_db.Config.Path, true);

                _vNode = null;
                _db = null;
            }
            catch (Exception)
            {

            }
        }

        protected void Publish(Message message)
        {
            _vNode.MainQueue.Publish(message);
        }
    }
}