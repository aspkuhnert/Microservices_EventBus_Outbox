﻿namespace BuildingBlocks.Domain.Events
{
    public abstract class Event :
       Message
    {
        public DateTime Timestamp { get; protected set; }

        protected Event()
        {
            Timestamp = DateTime.Now;
        }
    }
}