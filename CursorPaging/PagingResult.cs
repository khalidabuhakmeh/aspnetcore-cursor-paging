using System.Collections.Generic;

namespace CursorPaging
{
    public class PagingResult
    {
        public int Page { get; set; }
        public int Size { get; set; }
        public List<Picture> Pictures { get; set; }
        public int TotalCount { get; set; }
        
        public string Sql { get; set; }
    }
}