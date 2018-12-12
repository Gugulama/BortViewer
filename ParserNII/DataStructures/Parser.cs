using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ParserNII.DataStructures
{
    public abstract class Parser
    {
        public abstract List<DataFile> Parse(byte[] fileBytes);

        public DataArrays ToArray(List<DataFile> data)
        {
            var result = new DataArrays();

            var keys = data[0].Data.Keys;

            try
            {
                foreach (var key in keys)
                {
                    result.Data.Add(key, data.Select(d => d.Data[key]).ToArray());
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return result;
            }
        }

        protected string ParseCoordinate(int coordinate)
        {
            double coordDouble = coordinate * 0.000001000;
            return coordDouble.ToString();
        }
    }
}