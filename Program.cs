using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.FormRecognizer;
using Microsoft.Azure.CognitiveServices.FormRecognizer.Models;

namespace FormRecognizer
{
    class Program
    {
        // // Add your Azure Form Recognizer subscription key and endpoint to your environment variables.
        private static string subscriptionKey = Environment.GetEnvironmentVariable("COGNITIVE_SERVICE_KEY");
        private static string formRecognizerEndpoint = Environment.GetEnvironmentVariable("FORM_RECOGNIZER_ENDPOINT");

        // SAS Url to Azure Blob Storage container; this used for training the custom model
        // For help using SAS see: 
        // https://docs.microsoft.com/en-us/azure/storage/common/storage-dotnet-shared-access-signature-part-1
        private const string trainingDataUrl = "<AzureBlobSaS>";

        // Local path to a form to be analyzed
        // Any one or all of file formats (pdf,jpg or png)can be used with a trained model. 
        // For example,  
        //  pdf file  : "c:\documents\invoice.pdf" 
        //  jpeg file : "c:\documents\invoice.jpg"
        //  png file  : "c:\documents\invoice.png"
        private const string pdfFormFile = @"<pdfFormFileLocalPath>";
        private const string jpgFormFile = @"<jpgFormFileLocalPath>";
        private const string pngFormFile = @"<pngFormFileLocalPath>";

        static void Main(string[] args)
        {
            Console.WriteLine("====== Service Started ======");

            var t1 = RunFormRecognizerClient();
            Task.WaitAll(t1);
        }

        static async Task RunFormRecognizerClient()
        {
            // Create form client object with Form Recognizer subscription key
            IFormRecognizerClient formClient = new FormRecognizerClient(
                new ApiKeyServiceClientCredentials(subscriptionKey))
            {
                Endpoint = formRecognizerEndpoint
            };

            Console.WriteLine("Train Model with training data...");
            Guid modelId = await TrainModelAsync(formClient, trainingDataUrl);

            Console.WriteLine("Get list of extracted keys...");
            await GetListOfExtractedKeys(formClient, modelId);

            // Choose any of the following three Analyze tasks:

            Console.WriteLine("Analyze PDF form...");
            await AnalyzePdfForm(formClient, modelId, pdfFormFile);

            //Console.WriteLine("Analyze JPEG form...");
            //await AnalyzeJpgForm(formClient, modelId, jpgFormFile);

            //Console.WriteLine("Analyze PNG form...");
            //await AnalyzePngForm(formClient, modelId, pngFormFile);


            Console.WriteLine("Get list of trained models ...");
            await GetListOfModels(formClient);

            Console.WriteLine("Delete Model...");
            await DeleteModel(formClient, modelId);
        }

        
        // Train model using training form data (pdf, jpg, png files)
        private static async Task<Guid> TrainModelAsync(IFormRecognizerClient formClient, string trainingDataUrl)
        {
            if (!Uri.IsWellFormedUriString(trainingDataUrl, UriKind.Absolute))
            {
                Console.WriteLine("\nInvalid trainingDataUrl:\n{0} \n", trainingDataUrl);
                return Guid.Empty;
            }

            try
            {
                TrainResult result = await formClient.TrainCustomModelAsync(new TrainRequest(trainingDataUrl));

                ModelResult model = await formClient.GetCustomModelAsync(result.ModelId);
                DisplayModelStatus(model);

                return result.ModelId;
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("Train Model : " + e.Message);
                return Guid.Empty;
            }
        }

        
        // Display model status
        private static void DisplayModelStatus(ModelResult model)
        {
            Console.WriteLine("\nModel :");
            Console.WriteLine("\tModel id: " + model.ModelId);
            Console.WriteLine("\tStatus:  " + model.Status);
            Console.WriteLine("\tCreated: " + model.CreatedDateTime);
            Console.WriteLine("\tUpdated: " + model.LastUpdatedDateTime);
        }

        
        // Get and display list of extracted keys for training data 
        // provided to train the model
        private static async Task GetListOfExtractedKeys(IFormRecognizerClient formClient, Guid modelId)
        {
            if (modelId == Guid.Empty)
            {
                Console.WriteLine("\nInvalid model Id.");
                return;
            }

            try
            {
                KeysResult kr = await formClient.GetExtractedKeysAsync(modelId);
                var clusters = kr.Clusters;
                foreach (var kvp in clusters)
                {
                    Console.WriteLine("  Cluster: " + kvp.Key + "");
                    foreach (var v in kvp.Value)
                    {
                        Console.WriteLine("\t" + v);
                    }
                }
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("Get list of extracted keys : " + e.Message);
            }
        }

        
        // Analyze PDF form data
        private static async Task AnalyzePdfForm(IFormRecognizerClient formClient, Guid modelId, string pdfFormFile)
        {
            if (string.IsNullOrEmpty(pdfFormFile))
            {
                Console.WriteLine("\nInvalid pdfFormFile.");
                return;
            }

            try
            {
                using (FileStream stream = new FileStream(pdfFormFile, FileMode.Open))
                {
                    AnalyzeResult result = await formClient.AnalyzeWithCustomModelAsync(modelId, stream, contentType: "application/pdf");

                    Console.WriteLine("\nExtracted data from:" + pdfFormFile);
                    DisplayAnalyzeResult(result);
                }
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("Analyze PDF form : " + e.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Analyze PDF form : " + ex.Message);
            }
        }

        
        // Display analyze status
        private static void DisplayAnalyzeResult(AnalyzeResult result)
        {
            foreach (var page in result.Pages)
            {
                Console.WriteLine("\tPage#: " + page.Number);
                Console.WriteLine("\tCluster Id: " + page.ClusterId);
                foreach (var kv in page.KeyValuePairs)
                {
                    if (kv.Key.Count > 0)
                        Console.Write(kv.Key[0].Text);

                    if (kv.Value.Count > 0)
                        Console.Write(" - " + kv.Value[0].Text);

                    Console.WriteLine();
                }
                Console.WriteLine();

                foreach (var t in page.Tables)
                {
                    Console.WriteLine("Table id: " + t.Id);
                    foreach (var c in t.Columns)
                    {
                        foreach (var h in c.Header)
                            Console.Write(h.Text + "\t");

                        foreach (var e in c.Entries)
                        {
                            foreach (var ee in e)
                                Console.Write(ee.Text + "\t");
                        }
                        Console.WriteLine();
                    }
                    Console.WriteLine();
                }
            }
        }


        // Get and display list of trained the models
        private static async Task GetListOfModels(IFormRecognizerClient formClient)
        {
            try
            {
                ModelsResult models = await formClient.GetCustomModelsAsync();
                foreach (ModelResult m in models.ModelsProperty)
                {
                    Console.WriteLine(m.ModelId + " " + m.Status + " " + m.CreatedDateTime + " " + m.LastUpdatedDateTime);
                }
                Console.WriteLine();
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("Get list of models : " + e.Message);
            }
        }


        // Delete a model
        private static async Task DeleteModel(IFormRecognizerClient formClient, Guid modelId)
        {
            try
            {
                Console.Write("Deleting model: {0}...", modelId.ToString());
                await formClient.DeleteCustomModelAsync(modelId);
                Console.WriteLine("done.\n");
            }
            catch (ErrorResponseException e)
            {
                Console.WriteLine("Delete model : " + e.Message);
            }
        }
    
       
    }
}
