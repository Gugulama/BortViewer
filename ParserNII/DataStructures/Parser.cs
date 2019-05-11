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
            for (int i = 1; i < data.Count; i++)
            {
                if (data[i].Data.Count > keys.Count)
                {
                    keys = data[i].Data.Keys;
                }
            }

            foreach (var key in keys)
            {
                DataElement[] arr = new DataElement[data.Count];
                for (int i = 0; i < data.Count; i++)
                {
                    try
                    {
                        arr[i] = data[i].Data[key];
                    }
                    catch (KeyNotFoundException e)
                    {                        
                        arr[i] = new DataElement
                        {
                            ChartValue = 0,
                            DisplayValue = "0",
                            Display = true
                        };
                    }
                }
                result.Data.Add(key, arr);
            }
            return result;

            //try
            //{
            //    foreach (var key in keys)
            //    {
            //        result.Data.Add(key, data.Select(d => d.Data[key]).ToArray());
            //    }

                //    return result;
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine(ex);
                //    return result;
                //}
        }

        protected string ParseCoordinate(int coordinate)
        {
            double coordDouble = coordinate * 0.000001000;
            return coordDouble.ToString();
        }
    }
}