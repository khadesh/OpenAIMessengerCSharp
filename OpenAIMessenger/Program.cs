using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenAIMessenger
{
	internal class Program
	{
		public static string openAIBearerToken = "YOURBEARERTOKENHERE";
		public static string assistantID = "YOURASSISTANTIDHERE";

		/// <summary>
		/// This is a sample program for interacting with an OpenAI assistant end to end in the C# language.
		/// 
		/// The first message for a given object will start a new thread and a run.
		/// 
		/// Each message after that will generate a new message and run but within the same thread context. 
		/// 
		/// The response is simply the AI's string response.
		/// </summary>
		static async Task Main(string[] args)
		{
			// Example sending an ai thread two messages and printing those responses to the console:
			OpenAIMessenger messenger = new OpenAIMessenger(openAIBearerToken, assistantID);

			var response1 = await messenger.SendMessage("How are you today?");
			Console.WriteLine("AI Response: " + response1);

			var response2 = await messenger.SendMessage("Would you like to be my friend?");
			Console.WriteLine("AI Response: " + response2);

			Console.ReadKey();
		}

		public class OpenAIMessenger
		{
			#region Properties
			public string OpenAIBearerToken { get; set; }
			public string AssistantID { get; set; }
			public string ThreadID { get; set; }
			public string RunID { get; set; }
			#endregion

			#region Helper Classes
			public OpenAIMessenger(string openAIBearerToken, string assistantID)
			{
				this.OpenAIBearerToken = openAIBearerToken;
				this.AssistantID = assistantID;
			}

			public class ThreadCreateRunResponse
			{
				public string id { get; set; }
				public string thread_id { get; set; }
				public string status { get; set; }
			}
			public class MessageListResponse
			{
				public MessageListDataResponse[] data { get; set; }
			}

			public class MessageListDataResponse
			{
				public string id { get; set; }
				public string created_at { get; set; }
				public string assistant_id { get; set; }
				public string thread_id { get; set; }
				public string run_id { get; set; }
				public string role { get; set; }
				public MessageListDataContentResponse[] content { get; set; }
			}

			public class MessageListDataContentResponse
			{
				public string type { get; set; }
				public MessageListDataContentValueResponse text { get; set; }
			}

			public class MessageListDataContentValueResponse
			{
				public string value { get; set; }
			}
			#endregion

			#region Send Message
			public async Task<string> SendMessage(string message)
			{
				Console.WriteLine("Message to AI: " + message);
				if (this.RunID == null)
				{
					await CreateNewThreadRunWithMessage(message);
				}
				else
				{
					await AddMessageToRunThread(message);
					await CreateNewRunInThread();
				}
				DateTime startCheckStatus = DateTime.Now;
				while (await GetRunThreadStatus() != "completed")
				{
					Thread.Sleep(1000);
					if (DateTime.Now.Subtract(startCheckStatus).TotalSeconds >= 10.0)
					{
						return "Response timed out...";
					}
				}
				// then retrieve run results until completed...
				return await GetLastResponse();
			}
			#endregion

			#region Internal Calls To OpenAI API
			internal async Task<string> GetLastResponse()
			{
				if (this.ThreadID == null || this.RunID == null)
					throw new Exception("Must have a RunID and ThreadID to check the status of a run thread.");
				string apiUrl = $"https://api.openai.com/v1/threads/{this.ThreadID}/messages";
				using (HttpClient client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.OpenAIBearerToken}");
					client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
					try
					{
						HttpResponseMessage response = await client.GetAsync(apiUrl);
						response.EnsureSuccessStatusCode();
						string responseContent = await response.Content.ReadAsStringAsync();
						var results = JsonConvert.DeserializeObject<MessageListResponse>(responseContent);
						return results.data[0].content[0].text.value;
					}
					catch (HttpRequestException e)
					{

					}
				}
				return "";
			}

			internal async Task CreateNewRunInThread()
			{
				string apiUrl = $"https://api.openai.com/v1/threads/{this.ThreadID}/runs";
				using (HttpClient client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.OpenAIBearerToken}");
					client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
					var payload = new { assistant_id = this.AssistantID };
					string jsonPayload = JsonConvert.SerializeObject(payload);
					HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
					try
					{
						HttpResponseMessage response = await client.PostAsync(apiUrl, content);
						response.EnsureSuccessStatusCode();
						string responseContent = await response.Content.ReadAsStringAsync();
						var results = JsonConvert.DeserializeObject<ThreadCreateRunResponse>(responseContent);
						if (results != null)
						{
							this.RunID = results.id;
							this.ThreadID = results.thread_id;
						}
					}
					catch (HttpRequestException e)
					{

					}
				}
			}

			internal async Task AddMessageToRunThread(string message)
			{
				if (this.ThreadID == null || this.RunID == null)
					throw new Exception("Must have a RunID and ThreadID to check the status of a run thread.");
				string apiUrl = $"https://api.openai.com/v1/threads/{this.ThreadID}/messages";
				using (HttpClient client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.OpenAIBearerToken}");
					client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
					var payload = new { role = "user", content = message };
					string jsonPayload = JsonConvert.SerializeObject(payload);
					HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
					try
					{
						HttpResponseMessage response = await client.PostAsync(apiUrl, content);
						response.EnsureSuccessStatusCode();
						string responseContent = await response.Content.ReadAsStringAsync();
						var results = JsonConvert.DeserializeObject<MessageListResponse>(responseContent);
					}
					catch (HttpRequestException e)
					{

					}
				}
			}

			internal async Task<string> GetRunThreadStatus()
			{
				if (this.ThreadID == null || this.RunID == null)
					throw new Exception("Must have a RunID and ThreadID to check the status of a run thread.");
				string apiUrl = $"https://api.openai.com/v1/threads/{this.ThreadID}/runs/{this.RunID}";
				using (HttpClient client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.OpenAIBearerToken}");
					client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
					try
					{
						HttpResponseMessage response = await client.PostAsync(apiUrl, null);
						response.EnsureSuccessStatusCode();
						string responseContent = await response.Content.ReadAsStringAsync();
						var results = JsonConvert.DeserializeObject<ThreadCreateRunResponse>(responseContent);
						return results.status;
					}
					catch (HttpRequestException e)
					{

					}
				}
				return "";
			}

			internal async Task CreateNewThreadRunWithMessage(string message)
			{
				string apiUrl = "https://api.openai.com/v1/threads/runs";
				using (HttpClient client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.OpenAIBearerToken}");
					client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
					var payload = new
					{
						assistant_id = this.AssistantID,
						thread = new
						{
							messages = new dynamic[] { new { role = "user", content = message } }
						}
					};
					string jsonPayload = JsonConvert.SerializeObject(payload);
					HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
					try
					{
						HttpResponseMessage response = await client.PostAsync(apiUrl, content);
						response.EnsureSuccessStatusCode();
						string responseContent = await response.Content.ReadAsStringAsync();
						var results = JsonConvert.DeserializeObject<ThreadCreateRunResponse>(responseContent);
						if (results != null)
						{
							this.RunID = results.id;
							this.ThreadID = results.thread_id;
						}
					}
					catch (HttpRequestException e)
					{

					}
				}
			}
			#endregion
		}
	}
}
