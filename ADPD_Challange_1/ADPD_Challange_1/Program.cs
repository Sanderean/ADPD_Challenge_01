using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

class Program
{
    static async Task Main(string[] args)
    {
        IWebDriver driver = new ChromeDriver();

        try
        {
            string mainUrl = "https://xenabrowser.net/datapages/?hub=https://tcga.xenahubs.net:443";
            driver.Navigate().GoToUrl(mainUrl);

            await Task.Delay(5000);

            var cohortLinkElements = driver.FindElements(By.XPath("//a[contains(@href, 'cohort=TCGA')]"));
            List<string> cohortUrls = new List<string>();

            foreach (var cohortLinkElement in cohortLinkElements)
            {
                string cohortUrl = cohortLinkElement.GetAttribute("href");
                cohortUrls.Add(cohortUrl);
            }

            Console.WriteLine($"\nFound {cohortUrls.Count} cohort links.\n");

            foreach(var cohortUrl in cohortUrls)
            {
                Console.WriteLine($"\nNavigating to cohort page: {cohortUrl}\n");

                driver.Navigate().GoToUrl(cohortUrl);
                await Task.Delay(5000);

                var IlluminaHiSeqLinks = driver.FindElements(By.XPath("//a[contains(text(), 'IlluminaHiSeq pancan normalized')]"));

                Console.WriteLine("***************************************************");
                Console.WriteLine($"\n Found {IlluminaHiSeqLinks.Count} IlluminaHiSeq pancan normalized files \n");
                Console.WriteLine("***************************************************");

                if (IlluminaHiSeqLinks.Count > 0)
                {
                    foreach (var link in IlluminaHiSeqLinks)
                    {
                        Console.WriteLine("Found the link. Navigating...");


                        link.Click();
                        await Task.Delay(5000);

                        var downloadLink = driver.FindElement(By.XPath("//a[contains(@href, '.gz')]"));

                        if (downloadLink != null)
                        {
                            string fileUrl = downloadLink.GetAttribute("href");
                            Console.WriteLine($"Download link found: {fileUrl}");

                            using (HttpClient client = new HttpClient())
                            {
                                await DownloadFile(client, fileUrl);
                            }
                        }
                        else
                        {
                            Console.WriteLine("There is no .gz file at this link...");
                        }
                    }

                    Console.WriteLine("\nAll downloads are done for this cohort.\n");
                }
                else
                {
                    Console.WriteLine("***************************************************");
                    Console.WriteLine("\nNo IlluminaHiSeq pancan normalized files found for this cohort.\n");
                    Console.WriteLine("***************************************************");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            driver.Quit();
        }
    }

    private static async Task DownloadFile(HttpClient client, string fileUrl)
    {
        string fileName = Path.GetFileName(fileUrl);
        string workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "DownloadedFiles");

        string gzFilePath = Path.Combine(workingDirectory, fileName);

        client.Timeout = TimeSpan.FromMinutes(10);

        Console.WriteLine($"\nStarting download: {fileName}\n");

        try
        {
            using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(gzFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    var isMoreToRead = true;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        while (isMoreToRead)
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fs.WriteAsync(buffer, 0, read);
                                totalRead += read;

                                if (totalBytes != -1)
                                {
                                    Console.WriteLine($"Downloaded {totalRead} of {totalBytes} bytes ({(totalRead * 100.0 / totalBytes):0.00}%).");
                                }
                                else
                                {
                                    Console.WriteLine($"Downloaded {totalRead} bytes.");
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("\nDownload is successful\n");
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Console.WriteLine("Download timed out. The file might be too large or the server is slow.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while downloading the file: {ex.Message}");
        }
    }


}