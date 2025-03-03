﻿using LockManager;
using LogManager;
using NUnit.Framework;
using PageManager;
using PageManager.Exceptions;
using System.IO;
using System.Threading.Tasks;
using Test.Common;

namespace LogManagerTests
{
    public class TranLockTests
    {
        private ILogManager logManager;
        private IPageManager pageManager;

        [SetUp]
        public void Setup()
        {
            Stream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            pageManager =  new PageManager.PageManager(4096, TestGlobals.DefaultEviction, TestGlobals.DefaultPersistedStream);
            logManager = new LogManager.LogManager(writer);
        }

        [Test]
        public async Task LockCheck()
        {
            await using ITransaction tran1 = logManager.CreateTransaction(pageManager);
            using var releaser = await tran1.AcquireLock(1, LockTypeEnum.Shared);
            Assert.IsTrue(tran1.VerifyLock(1, LockTypeEnum.Shared));
        }

        [Test]
        public async Task LockCheckDowngrade()
        {
            await using ITransaction tran1 = logManager.CreateTransaction(pageManager);
            using var releaser = await tran1.AcquireLock(1, LockTypeEnum.Exclusive);
            Assert.IsTrue(tran1.VerifyLock(1, LockTypeEnum.Shared));
        }

#if DEBUG
        [Test]
        public async Task LockCheckUpgrade()
        {
            await using ITransaction tran1 = logManager.CreateTransaction(pageManager);
            using var releaser = await tran1.AcquireLock(1, LockTypeEnum.Shared);
            Assert.IsFalse(tran1.VerifyLock(1, LockTypeEnum.Exclusive));
        }
#endif

        [Test]
        public async Task LockNotReleased()
        {
            await using ITransaction tran1 = logManager.CreateTransaction(pageManager);
            using var releaser = await tran1.AcquireLock(1, LockTypeEnum.Shared);
            Assert.Throws<TranHoldingLockDuringDispose>(() => tran1.Dispose());
        }

        [Test]
        public async Task AcquireLoop()
        {
            await using ITransaction tran1 = logManager.CreateTransaction(pageManager);

            for (int i = 0; i < 1000; i++)
            {
                using var releaser = await tran1.AcquireLock((ulong)i, LockTypeEnum.Shared);
                Assert.IsTrue(tran1.VerifyLock((ulong)i, LockTypeEnum.Shared));
            }
        }
    }
}
