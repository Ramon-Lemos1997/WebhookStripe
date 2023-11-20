using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Text;


namespace StripeWebhook.Controllers
{
    public class StripeController : Controller
    {
        private readonly Queue<int> requestQueue = new Queue<int>();
        private readonly IConfiguration _configuration;
        public StripeController()
        {
        }

        [HttpPost("/webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var apiKey = _configuration["Stripe:ApiKey"];
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            try
            {
                StripeConfiguration.ApiKey = apiKey;
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret,
                    throwOnApiVersionMismatch: false
                );

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        var session = stripeEvent.Data.Object as Session;
                        var metadataCollection = session.Metadata;

                        foreach (var metadataItem in metadataCollection.Values)
                        {
                            int orderId = int.Parse(metadataItem);
                            requestQueue.Enqueue(orderId);
                        }

                        // Após adicionar os itens à fila, inicia o processamento da fila
                        await ProcessQueue();

                        break;
                    
                    default:
                        Console.WriteLine($"Unhandled event type {stripeEvent.Type}");
                        break;
                }

                return Ok();
            }
            catch (StripeException e)
            {
                Console.WriteLine($"StripeException: {e}");
                return BadRequest();
            }
        }

        private async Task ProcessQueue()
        {
            // Verifica se há algo na fila ou se estamos processando algo
            if (requestQueue.Count == 0)
            {
                return;
            }

            // Remove o primeiro item da fila e atribui seu valor a metadata
            var metadata = requestQueue.Dequeue();

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var apiUrl = "https://localhost:7034/Payment/Payment/HandleOrder"; 
                    var content = new StringContent(metadata.ToString(), Encoding.UTF8, "application/json");

                    // Envia uma solicitação POST para a API
                    var response = await httpClient.PostAsync(apiUrl, content);

                    // Verifica se a solicitação foi bem-sucedida
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Post enviado com sucesso!");
                    }
                    else
                    {
                        Console.WriteLine($"Erro ao enviar POST. Status: {response.StatusCode}");
                    }
                }
                Console.WriteLine(metadata);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar a fila: {ex.Message}");
            }
            finally
            {
                // Chama ProcessQueue() novamente para processar o próximo item da fila, se houver
                await ProcessQueue();
            }
        }

        //----------------------------------------------------------------------------------------------------

    }
}

       

        

        
