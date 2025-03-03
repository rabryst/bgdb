﻿using PageManager;
using System.IO;
using System.Threading.Tasks;

namespace LogManager
{
    public class AllocatePageLogRecord : ILogRecord
    {
        public readonly ulong PageId;
        public readonly ulong TranId;
        public readonly PageType PageType;
        public readonly uint PageSize;
        public readonly ulong NextPageId;
        public readonly ulong PrevPageId;
        public readonly ColumnInfo[] ColumnTypes;

        public LogRecordType GetRecordType() => LogRecordType.AllocatePage;

        public AllocatePageLogRecord(ulong pageId, ulong transactionid, PageType pageType, uint pageSize, ulong nextPageId, ulong prevPageId, ColumnInfo[] columnTypes) =>
            (PageId, TranId, PageType, PageSize, NextPageId, PrevPageId, ColumnTypes) = (pageId, transactionid, pageType, pageSize, nextPageId, prevPageId, columnTypes);

        public AllocatePageLogRecord(BinaryReader br)
        {
            this.PageId = br.ReadUInt64();
            this.TranId = br.ReadUInt64();
            this.PageType = (PageType)br.ReadUInt16();
            this.PageSize = br.ReadUInt32();
            this.NextPageId = br.ReadUInt64();
            this.PrevPageId = br.ReadUInt64();
            ushort columnTypesLen = br.ReadUInt16();

            if (columnTypesLen != 0)
            {
                this.ColumnTypes = new ColumnInfo[columnTypesLen];

                for (int i = 0; i < columnTypesLen; i++)
                {
                    this.ColumnTypes[i] = ColumnInfo.Deserialize(br);
                }
            }
        }

        public RedoContent GetRedoContent()
        {
            throw new System.NotImplementedException();
        }

        public UndoContent GetUndoContent()
        {
            throw new System.NotImplementedException();
        }

        public async Task Redo(IPageManager pageManager, ITransaction tran)
        {
            await pageManager.AllocatePage(this.PageType, this.ColumnTypes, this.PrevPageId, this.NextPageId, this.PageId, tran).ConfigureAwait(false);
        }

        public void Serialize(BinaryWriter destination)
        {
            destination.Write((byte)LogRecordType.AllocatePage);
            destination.Write(this.PageId);
            destination.Write(this.TranId);
            destination.Write((ushort)this.PageType);
            destination.Write(this.PageSize);
            destination.Write(this.NextPageId);
            destination.Write(this.PrevPageId);

            if (this.ColumnTypes == null)
            {
                destination.Write((ushort)0);
            }
            else
            {
                destination.Write((ushort)this.ColumnTypes.Length);
                foreach (ColumnInfo ct in this.ColumnTypes)
                {
                    ct.Serialize(destination);
                }
            }
        }

        public ulong TransactionId() => this.TranId;

        public Task Undo(IPageManager pageManager, ITransaction tran) => Task.CompletedTask;
    }
}
