using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ParserNII.DataStructures
{
    public class BinFileParser : Parser
    {

        public override List<DataFile> Parse(byte[] fileBytes)
        {
            List<BinFile> result = new List<BinFile>();
            List<byte[]> dataChunks = Split(fileBytes);
            double timeNowEpoch = Convert.ToInt64(DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
            for (int i = 0; i < dataChunks.Count; i++)
            {
                long time = BitConverter.ToInt64(dataChunks[i], 0);
                int uid = BitConverter.ToInt32(dataChunks[i], 8);
                double value = BitConverter.ToDouble(dataChunks[i], 12);
                
                if ((time > (timeNowEpoch - 31556926000)) && (time < (timeNowEpoch + 86400000))) // Запас -1год +1 сутки
                {
                    if ((time % 1000 == 0) && (time % 3 == 0)) // Отсекаем кривые метки путем выявления времени кратной 3 секундам
                    {
                        if (Math.Abs(value) > 0.001 || value == 0) // Отсекаем кривые метки путем выявления чисел в степени овермного/овермало
                        {

                            if ((uid == 2 || uid == 6 || uid == 9 || uid == 19
                           || uid == 20 || uid == 50 || uid == 101
                           || uid == 3101 || uid == 3102 || uid == 3104)
                           && (value > 127))
                            {
                                value -= 256;
                            }

                            result.Add(new BinFile
                            {
                                Date = time,
                                Uid = uid,
                                Value = value
                            });
                        }
                    }
                }
            }

            return ToDataFile(result);
        }

        private List<byte[]> Split(byte[] fileBytes)
        {
            var source = fileBytes.ToList();
            var result = new List<byte[]>();

            try
            {
                for (int i = 0; i < fileBytes.Length; i += 20)
                {
                    result.Add(source.GetRange(i, 20).ToArray());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("-------------------------------------------------------------");
                Console.WriteLine(ex.Message);
                Console.WriteLine("-------------------------------------------------------------");
            }

            return result;
        }

        private List<DataFile> ToDataFile(List<BinFile> data)
        {
            Dictionary<int, ConfigElement> binFileParams = Config.Instance.binFileParams.ToDictionary(d => d.number);

            var result = new List<DataFile>();
            var dates = data.GroupBy(d => d.Date).ToArray();

            bool first = true;

            foreach (var date in dates)
            {
                List<BinFile> elementsOfDate = date.ToList();
                var resultElement = new DataFile();                
                resultElement.Data.Add("Время в “UNIX” формате", new DataElement { OriginalValue = date.Key, DisplayValue = DateTimeOffset.FromUnixTimeMilliseconds(date.Key).AddHours(3).ToString("dd.MM.yyyy HH:mm:ss") });

                for (int j = 0; j < elementsOfDate.Count; j++)
                {
                    var element = elementsOfDate[j];

                    try
                    {
                        var dataElement = new DataElement
                        {
                            ChartValue = element.Value,
                            DisplayValue = element.Value.ToString(),
                            Display = true,
                            DataParams = binFileParams[element.Uid]
                        };
                        resultElement.Data.Add(dataElement.DataParams.name, dataElement);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("------------------------------------------ Отсутствует конфигурация параметра ID: " + element.Uid);
                    }
                }

                if (!first && result.Last().Data.Count > resultElement.Data.Count)
                {
                    var keys = result.Last().Data.Keys;
                    foreach (var key in keys)
                    {
                        if (!resultElement.Data.ContainsKey(key))
                        {
                            resultElement.Data.Add(key, result.Last().Data[key]);
                        }
                    }
                }
                

                result.Add(resultElement);

                first = false;
            }

            return result;
        }
    }
}