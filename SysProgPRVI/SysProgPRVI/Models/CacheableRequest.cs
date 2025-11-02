//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Threading;
//using System.Threading.Tasks;

namespace SysProgPRVI.Models
{
    internal class CacheableRequest
    {
            private readonly string httpsRequest;
            private int numOfHits; //za Least frequently used algoritam

            public string HttpsRequest { get { return httpsRequest; } }
            public int NumOfHits
            {
                get { return numOfHits; }
                set { numOfHits = value; }
            }

            public CacheableRequest(string httpsRequest, int numOfHits = 0)
            {
                this.httpsRequest = httpsRequest;
                this.numOfHits = numOfHits;
            }

            public void incrementHit() { Interlocked.Increment(ref numOfHits); }
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                    return false;

                CacheableRequest other = obj as CacheableRequest;
                return this.httpsRequest == other.httpsRequest;
            }


            public override int GetHashCode()
            {
                return httpsRequest.GetHashCode();
            }
        }
    }
