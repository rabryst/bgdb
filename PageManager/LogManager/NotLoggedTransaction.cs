﻿using LockManager;
using LockManager.LockImplementation;
using PageManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogManager
{
    public class NotLoggedTransaction : ITransaction
    {
        public async Task<Releaser> AcquireLock(ulong pageId, LockTypeEnum lockType)
        {
            return new Releaser();
        }

        public void AddRecord(ILogRecord logRecord)
        {
        }

        public Task Commit()
        {
            throw new InvalidOperationException("Not logged tran can't be committed");
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => default;

        public IEnumerable<ILogRecord> GetRecords() => Enumerable.Empty<ILogRecord>();

        public TransactionState GetTransactionState() => TransactionState.Open;

        public Task Rollback()
        {
            throw new InvalidOperationException("Not logged tran can't be rolled back");
        }

        public ulong TranscationId() => 0;

        public void VerifyLock(ulong pageId, LockTypeEnum expectedLock)
        {
        }
    }
}
