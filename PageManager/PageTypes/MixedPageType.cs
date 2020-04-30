﻿using System;
using System.Runtime.Serialization;

namespace PageManager
{
    public interface IAllocateMixedPage
    {
        MixedPage AllocateMixedPage(ColumnType[] columnTypes);
        MixedPage GetMixedPage(ulong pageId);
    }

    public class MixedPage : IPageSerializer<RowsetHolder>
    {
        private readonly uint pageSize;
        private readonly ulong pageId;

        protected byte[] content;

        private readonly ColumnType[] columnTypes;

        public MixedPage(uint pageSize, ulong pageId, ColumnType[] columnTypes)
        {
            if (columnTypes == null || columnTypes.Length == 0)
            {
                throw new ArgumentException("Column type definition can't be null or empty");
            }

            this.pageSize = pageSize;
            this.pageId = pageId;

            this.content = new byte[pageSize];
            this.columnTypes = columnTypes;
        }

        public byte[] GetContent() => this.content;

        public ulong PageId() => this.pageId;

        public PageType PageType() => PageManager.PageType.MixedPage;

        public uint SizeInBytes() => this.pageSize;

        public void Serialize(RowsetHolder items)
        {
            uint neededSize = this.GetSizeNeeded(items);
            if (!this.CanFit(items))
            {
                throw new SerializationException();
            }

            uint contentPosition = 0;
            foreach (byte pageByte in BitConverter.GetBytes(this.pageId))
            {
                content[contentPosition] = pageByte;
                contentPosition++;
            }

            foreach (byte sizeByte in BitConverter.GetBytes(this.pageSize))
            {
                content[contentPosition] = sizeByte;
                contentPosition++;
            }

            foreach (byte typeByte in BitConverter.GetBytes((int)PageManager.PageType.MixedPage))
            {
                content[contentPosition] = typeByte;
                contentPosition++;
            }

            foreach (byte numOfRowsByte in BitConverter.GetBytes(neededSize))
            {
                content[contentPosition] = numOfRowsByte;
                contentPosition++;
            }

            SerializeInternal(items);
        }

        private void SerializeInternal(IRowsetHolder item)
        {
            item.SerializeInto(this.content.AsSpan((int)IPage.FirstElementPosition));
        }

        private uint GetSizeNeeded(IRowsetHolder items)
        {
            return items.StorageSizeInBytes();
        }

        public RowsetHolder Deserialize()
        {
            RowsetHolder rowsetHolder = new RowsetHolder(this.columnTypes);

            int elemCount = BitConverter.ToInt32(this.content, (int)IPage.FirstElementPosition);
            int size = (int)(elemCount * RowsetHolder.CalculateSizeOfRow(this.columnTypes) + sizeof(int));
            rowsetHolder.Deserialize(this.content.AsSpan((int)IPage.FirstElementPosition, size));

            return rowsetHolder;
        }

        public uint MaxRowCount()
        {
            return (this.pageSize - IPage.FirstElementPosition - sizeof(int)) / RowsetHolder.CalculateSizeOfRow(this.columnTypes);
        }

        public bool CanFit(RowsetHolder items)
        {
            return this.pageSize - IPage.FirstElementPosition >= items.StorageSizeInBytes();
        }
    }
}
