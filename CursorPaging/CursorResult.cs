using System.Collections.Generic;

namespace CursorPaging
{
    public class CursorResult
    {
        public CursorItems Cursor { get; set; } = new();
        public List<Picture> Pictures { get; set; }
        public int TotalCount { get; set; }
        
        public class CursorItems
        {
            public int? Before { get; set; }
            public int? After { get; set; }
        }
        
        public string Sql { get; set; }
    }
}