﻿using System;
using System.IO;

namespace PageManager
{
    public interface IAllocateIntegerPage
    {
        IntegerOnlyPage AllocatePageInt(ulong prevPage, ulong nextPage, ITransaction transaction);
        IntegerOnlyPage GetPageInt(ulong pageId, ITransaction transaction);
    }

    public class IntegerOnlyPage : SimpleTypeOnlyPage<int>
    {
        public IntegerOnlyPage(uint pageSize, ulong pageId, ulong prevPageId, ulong nextPageId, ITransaction transaction) 
            : base(pageSize, pageId, PageManager.PageType.IntPage, prevPageId, nextPageId, transaction) { }
        public IntegerOnlyPage(BinaryReader stream) : base(stream, PageManager.PageType.IntPage) { }

        protected override void SerializeInternal(BinaryReader stream)
        {
            this.items = new int[this.rowCount];

            for (int i = 0; i < this.items.Length; i++)
            {
                this.items[i] = stream.ReadInt32();
            }
        }

        public override void Persist(Stream destination)
        {
            using (BinaryWriter bw = new BinaryWriter(destination))
            {
                bw.Write(this.pageId);
                bw.Write(this.pageSize);
                bw.Write((int)this.PageType());
                bw.Write(this.rowCount);
                bw.Write(this.prevPageId);
                bw.Write(this.nextPageId);

                for (int i = 0; i < this.rowCount; i++)
                {
                    bw.Write(this.items[i]);
                }
            }
        }

        public override void RedoLog(ILogRecord record, ITransaction tran)
        {
            if (record.GetRecordType() == LogRecordType.RowModify)
            {
                var redoContent = record.GetRedoContent();
                this.items[redoContent.RowPosition] = BitConverter.ToInt32(redoContent.DataToApply);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override void UndoLog(ILogRecord record, ITransaction tran)
        {
            if (record.GetRecordType() == LogRecordType.RowModify)
            {
                var undoContent = record.GetUndoContent();
                this.items[undoContent.RowPosition] = BitConverter.ToInt32(undoContent.DataToUndo);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
