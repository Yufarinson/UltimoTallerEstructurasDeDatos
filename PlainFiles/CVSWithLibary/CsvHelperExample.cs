using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace CVSWithLibary;

public class CsvHelperExample
{
    public void Write(string path, IEnumerable<Person> people)
    {
        using var sw = new StreamWriter(path);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };
        using var cw = new CsvWriter(sw, config);
        cw.WriteRecords(people);
    }

    public IEnumerable<Person> Read(string path)
    {
        if (!File.Exists(path))
        {
            try
            {
                using (var sw = new StreamWriter(path))
                using (var cw = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    cw.WriteHeader<Person>();
                    cw.NextRecord(); 
                }
                return Enumerable.Empty<Person>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating or writing header to {path}: {ex.Message}");
                return Enumerable.Empty<Person>(); 
            }
        }

        using var sr = new StreamReader(path);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };
        using var cr = new CsvReader(sr, config);
        try
        {
            sr.BaseStream.Position = 0; 
            sr.DiscardBufferedData();   
            var firstChar = sr.Peek();
            if (firstChar == -1) 
            {
                sr.BaseStream.Position = 0; sr.DiscardBufferedData(); 
                                                                     
                using (var sw = new StreamWriter(path, false)) 
                using (var cw = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    cw.WriteHeader<Person>();
                    cw.NextRecord();
                }
                return Enumerable.Empty<Person>();
            }

       
            sr.BaseStream.Position = 0; sr.DiscardBufferedData();
            var tempReaderForHeaderCheck = new CsvReader(new StreamReader(path), CultureInfo.InvariantCulture);
            tempReaderForHeaderCheck.Read(); 
            bool hasRecords = tempReaderForHeaderCheck.Read();
            tempReaderForHeaderCheck.Dispose();

            if (!hasRecords)
            {
                sr.BaseStream.Position = 0; sr.DiscardBufferedData(); 
                return Enumerable.Empty<Person>();
            }

            sr.BaseStream.Position = 0;
            sr.DiscardBufferedData();

            return cr.GetRecords<Person>().ToList();
        }
        catch (CsvHelper.HeaderValidationException)
        {
            if (new FileInfo(path).Length == 0)
            {
                using (var sw = new StreamWriter(path, false)) 
                using (var cw = new CsvWriter(sw, CultureInfo.InvariantCulture))
                {
                    cw.WriteHeader<Person>();
                    cw.NextRecord();
                }
                return Enumerable.Empty<Person>();
            }
            Console.WriteLine($"CSV Header Validation Error in {path}. Ensure the file has correct headers or is not corrupted.");
            return Enumerable.Empty<Person>();
        }
        catch (CsvHelper.CsvHelperException ex) 
        {
            Console.WriteLine($"Error reading CSV file {path}: {ex.Message}");
            return Enumerable.Empty<Person>();
        }
    }
}