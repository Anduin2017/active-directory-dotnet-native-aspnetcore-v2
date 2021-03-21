// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TodoListClient
{
    public partial class MainWindow : Window
    {
        private static readonly string TodoListBaseAddress = ConfigurationManager.AppSettings["todo:TodoListBaseAddress"];
        private static string TodoListApiAddress
        {
            get
            {
                string baseAddress = TodoListBaseAddress;
                return baseAddress.EndsWith("/") ? TodoListBaseAddress + "api/todolist"
                                                 : TodoListBaseAddress + "/api/todolist";
            }
        }

        private TokenService _tokenService;
        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            _tokenService = new TokenService();

            _ = GetTodoList(true).ConfigureAwait(false);
        }

        private async Task GetTodoList(bool ignoreIfUnauthorized)
        {
            try
            {
                var user = await this._tokenService.GetUser(!ignoreIfUnauthorized);
                SetUserName(user.Account);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
                var response = await _httpClient.GetAsync(TodoListApiAddress);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var toDoArray = JsonConvert.DeserializeObject<List<TodoItem>>(json);
                    TodoList.ItemsSource = toDoArray.Select(t => new { t.Title });
                }
                else
                {
                    await DisplayErrorMessage(response);
                }
            }
            catch (UnauthorizedException) { }
        }

        private static async Task DisplayErrorMessage(HttpResponseMessage httpResponse)
        {
            string failureDescription = await httpResponse.Content.ReadAsStringAsync();
            if (failureDescription.StartsWith("<!DOCTYPE html>"))
            {
                string path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                string errorFilePath = Path.Combine(path, "error.html");
                File.WriteAllText(errorFilePath, failureDescription);
                Process.Start(errorFilePath);
            }
            else
            {
                MessageBox.Show($"{httpResponse.ReasonPhrase}\n {failureDescription}", "An error occurred while getting /api/todolist", MessageBoxButton.OK);
            }
        }

        private async void AddTodoItem(object sender, RoutedEventArgs e)
        {
            var user = await this._tokenService.GetUser(true);
            SetUserName(user.Account);

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
            var todoItem = new TodoItem() { Title = TodoText.Text };
            var json = JsonConvert.SerializeObject(todoItem);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(TodoListApiAddress, content);
            if (response.IsSuccessStatusCode)
            {
                TodoText.Text = "";
                await GetTodoList(false);
            }
            else
            {
                await DisplayErrorMessage(response);
            }
        }

        private async void SignIn(object sender = null, RoutedEventArgs args = null)
        {
            var user = await this._tokenService.GetUser(true);
            SetUserName(user.Account);
        }

        // Set user name to text box
        private void SetUserName(IAccount userInfo)
        {
            string userName = null;

            if (userInfo != null)
            {
                userName = userInfo.Username;
            }

            if (userName == null)
                userName = Properties.Resources.UserNotIdentified;

            UserName.Content = userName;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await this._tokenService.Reset();
            SetUserName(null);
        }
    }
}
