﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using QBittorrent.Client.Extensions;

namespace QBittorrent.Client
{
    /// <summary>
    /// Provides access to qBittorrent remote API.
    /// </summary>
    /// <seealso cref="IDisposable" />
    /// <seealso cref="IQBittorrentClient"/>
    public class QBittorrentClient : IQBittorrentClient, IDisposable
    {
        private readonly Uri _uri;
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
        /// </summary>
        /// <param name="uri">qBittorrent remote server URI.</param>
        public QBittorrentClient([NotNull] Uri uri)
            : this(uri, new HttpClient())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
        /// </summary>
        /// <param name="uri">qBittorrent remote server URI.</param>
        /// <param name="handler">Custom HTTP message handler.</param>
        /// <param name="disposeHandler">The value indicating whether the <paramref name="handler"/> must be disposed when disposing this object.</param>
        public QBittorrentClient([NotNull] Uri uri, HttpMessageHandler handler, bool disposeHandler)
            : this(uri, new HttpClient(handler, disposeHandler))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
        /// </summary>
        /// <param name="uri">The qBittorrent remote server URI.</param>
        /// <param name="client">Custom <see cref="HttpClient"/> instance.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="uri"/> or <paramref name="client"/> is <see langword="null"/>.
        /// </exception>
        private QBittorrentClient([NotNull] Uri uri, [NotNull] HttpClient client)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #region Authentication

        /// <summary>
        /// Authenticates this client with the remote qBittorrent server.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task LoginAsync(
                    string username,
                    string password,
                    CancellationToken token = default)
        {
            var response = await _client.PostAsync(new Uri(_uri, "/login"),
                BuildForm(
                    ("username", username),
                    ("password", password)
                ),
                token)
                .ConfigureAwait(false);
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Clears authentication on the remote qBittorrent server.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task LogoutAsync(
                    CancellationToken token = default)
        {
            var response = await _client.PostAsync(new Uri(_uri, "/logout"), BuildForm(), token).ConfigureAwait(false);
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        #endregion

        #region Get

        /// <summary>
        /// Gets the torrent list.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<IReadOnlyList<TorrentInfo>> GetTorrentListAsync(
                    TorrentListQuery query = null,
                    CancellationToken token = default)
        {
            query = query ?? new TorrentListQuery();
            var uri = BuildUri("/query/torrents",
                ("filter", query.Filter.ToString().ToLowerInvariant()),
                ("category", query.Category),
                ("sort", query.SortBy),
                ("reverse", query.ReverseSort.ToLowerString()),
                ("limit", query.Limit?.ToString()),
                ("offset", query.Offset?.ToString()));

            var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<TorrentInfo[]>(json);
            return result;
        }

        /// <summary>
        /// Gets the torrent generic properties.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<TorrentProperties> GetTorrentPropertiesAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<TorrentProperties> ExecuteAsync()
            {
                var uri = BuildUri($"/query/propertiesGeneral/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentProperties>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent contents.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentContent>> GetTorrentContentsAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentContent>> ExecuteAsync()
            {
                var uri = BuildUri($"/query/propertiesFiles/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentContent[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent trackers.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentTracker>> ExecuteAsync()
            {
                var uri = BuildUri($"/query/propertiesTrackers/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentTracker[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the torrent web seeds.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<Uri>> GetTorrentWebSeedsAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<Uri>> ExecuteAsync()
            {
                var uri = BuildUri($"/query/propertiesWebSeeds/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<UrlItem[]>(json);
                return result.Select(x => x.Url).ToArray();
            }
        }

        /// <summary>
        /// Gets the states of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<TorrentPieceState>> GetTorrentPiecesStatesAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<TorrentPieceState>> ExecuteAsync()
            {
                var uri = BuildUri($"/query/getPieceStates/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<TorrentPieceState[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the hashes of the torrent pieces.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyList<string>> GetTorrentPiecesHashesAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<IReadOnlyList<string>> ExecuteAsync()
            {
                var uri = BuildUri($"/query/getPieceHashes/{hash}");
                var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
                var result = JsonConvert.DeserializeObject<string[]>(json);
                return result;
            }
        }

        /// <summary>
        /// Gets the global transfer information.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<GlobalTransferInfo> GetGlobalTransferInfoAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/query/transferInfo");
            var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<GlobalTransferInfo>(json);
            return result;
        }

        /// <summary>
        /// Gets the partial data.
        /// </summary>
        /// <param name="responseId">The response identifier.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<PartialData> GetPartialDataAsync(
                    int responseId = 0,
                    CancellationToken token = default)
        {
            var uri = BuildUri("/sync/maindata", ("rid", responseId.ToString()));
            var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<PartialData>(json);
            return result;
        }

        #endregion

        #region Add

        /// <summary>
        /// Adds the torrent files to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTorrentsAsync(
                    [NotNull] AddTorrentFilesRequest request,
                    CancellationToken token = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/upload");
                var data = new MultipartFormDataContent();
                foreach (var file in request.TorrentFiles)
                {
                    data.AddFile("torrents", file, "application/x-bittorrent");
                }

                await AddTorrentsCoreAsync(uri, data, request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds the torrent URLs or magnet-links to download.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTorrentsAsync(
                    [NotNull] AddTorrentUrlsRequest request,
                    CancellationToken token = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/download");
                var urls = string.Join("\n", request.TorrentUrls.Select(url => url.AbsoluteUri));
                var data = new MultipartFormDataContent().AddValue("urls", urls);
                await AddTorrentsCoreAsync(uri, data, request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds the torrents.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="data">The data.</param>
        /// <param name="request">The request.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected async Task AddTorrentsCoreAsync(
                            Uri uri,
                            MultipartFormDataContent data,
                            AddTorrentRequest request,
                            CancellationToken token)
        {
            data
                .AddNonEmptyString("savepath", request.DownloadFolder)
                .AddNonEmptyString("cookie", request.Cookie)
                .AddNonEmptyString("category", request.Category)
                .AddValue("skip_checking", request.SkipHashChecking)
                .AddValue("paused", request.Paused)
                .AddNotNullValue("root_folder", request.CreateRootFolder)
                .AddNonEmptyString("rename", request.Rename)
                .AddNotNullValue("upLimit", request.UploadLimit)
                .AddNotNullValue("dlLimit", request.DownloadLimit)
                .AddValue("sequentialDownload", request.SequentialDownload)
                .AddValue("firstLastPiecePrio", request.FirstLastPiecePrioritized);

            using (var response = await _client.PostAsync(uri, data, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        #endregion

        #region Pause/Resume

        /// <summary>
        /// Pauses the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task PauseAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/pause");
                var response = await _client.PostAsync(uri, BuildForm(("hash", hash)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task PauseAllAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/pauseAll");
            var response = await _client.PostAsync(uri, BuildForm(), token).ConfigureAwait(false);
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Resumes the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ResumeAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/resume");
                var response = await _client.PostAsync(uri, BuildForm(("hash", hash)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Resumes all torrents.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task ResumeAllAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/resumeAll");
            var response = await _client.PostAsync(uri, BuildForm(), token).ConfigureAwait(false);
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        #endregion

        #region Categories

        /// <summary>
        /// Adds the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddCategoryAsync(
                    [NotNull] string category,
                    CancellationToken token = default)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("The category cannot be empty.", nameof(category));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/addCategory");
                var response = await _client.PostAsync(uri, BuildForm(("category", category)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Deletes the category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteCategoryAsync(
                    [NotNull] string category,
                    CancellationToken token = default)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("The category cannot be empty.", nameof(category));

            return DeleteCategoriesAsync(new[] { category }, token);
        }

        /// <summary>
        /// Deletes the categories.
        /// </summary>
        /// <param name="categories">The list of categories' names.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteCategoriesAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> categories,
                    CancellationToken token = default)
        {
            var names = GetNames();
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/removeCategories");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(("categories", names)),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            string GetNames()
            {
                if (categories == null)
                    throw new ArgumentNullException(nameof(categories));

                var builder = new StringBuilder(4096);
                foreach (var category in categories)
                {
                    if (string.IsNullOrWhiteSpace(category))
                        throw new ArgumentException("The collection must not contain nulls or empty strings.", nameof(categories));

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(category);
                }

                if (builder.Length == 0)
                    throw new ArgumentException("The collection must contain at least one category.", nameof(categories));

                return builder.ToString();
            }
        }

        /// <summary>
        /// Sets the torrent category.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentCategoryAsync(
                    [NotNull] string hash,
                    [NotNull] string category,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setCategory");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hash),
                        ("category", category)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Sets the torrent category.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="category">The category.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentCategoryAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    [NotNull] string category,
                    CancellationToken token = default)
        {
            if (hashes == null)
                throw new ArgumentNullException(nameof(hashes));
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setCategory");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", string.Join("|", hashes)),
                        ("category", category)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        #endregion

        #region Limits

        /// <summary>
        /// Gets the torrent download speed limit.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<long?> GetTorrentDownloadLimitAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<long?> ExecuteAsync()
            {
                var dict = await GetTorrentDownloadLimitAsync(new[] { hash }, token).ConfigureAwait(false);
                return dict?.Values?.SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyDictionary<string, long>> GetTorrentDownloadLimitAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task<IReadOnlyDictionary<string, long>> ExecuteAsync()
            {
                var uri = BuildUri("/command/getTorrentsDlLimit");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(("hashes", hashesString)),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, long>>(json);
                    return dict;
                }
            }
        }

        /// <summary>
        /// Sets the torrent download speed limit.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentDownloadLimitAsync(
                    [NotNull] string hash,
                    long limit,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return SetTorrentDownloadLimitAsync(new[] { hash }, limit, token);
        }

        /// <summary>
        /// Sets the torrent download speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentDownloadLimitAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    long limit,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setTorrentsDlLimit");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(
                            ("hashes", hashesString),
                            ("limit", limit.ToString())),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Gets the torrent upload speed limit.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<long?> GetTorrentUploadLimitAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task<long?> ExecuteAsync()
            {
                var dict = await GetTorrentUploadLimitAsync(new[] { hash }, token).ConfigureAwait(false);
                return dict?.Values?.SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task<IReadOnlyDictionary<string, long>> GetTorrentUploadLimitAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task<IReadOnlyDictionary<string, long>> ExecuteAsync()
            {
                var uri = BuildUri("/command/getTorrentsUpLimit");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(("hashes", hashesString)),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, long>>(json);
                    return dict;
                }
            }
        }

        /// <summary>
        /// Sets the torrent upload speed limit.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentUploadLimitAsync(
                    [NotNull] string hash,
                    long limit,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return SetTorrentUploadLimitAsync(new[] { hash }, limit, token);
        }

        /// <summary>
        /// Sets the torrent upload speed limit.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetTorrentUploadLimitAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    long limit,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setTorrentsUpLimit");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(
                            ("hashes", hashesString),
                            ("limit", limit.ToString())),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Gets the global download speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<long?> GetGlobalDownloadLimitAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/getGlobalDlLimit");
            using (var response = await _client.PostAsync(uri, null, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var strValue = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return long.TryParse(strValue, out long value) ? value : 0;
            }
        }

        /// <summary>
        /// Sets the global download speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetGlobalDownloadLimitAsync(
                    long limit,
                    CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setGlobalDlLimit");
                var response = await _client.PostAsync(uri, BuildForm(("limit", limit.ToString())), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Gets the global upload speed limit.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<long?> GetGlobalUploadLimitAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/getGlobalUpLimit");
            using (var response = await _client.PostAsync(uri, null, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var strValue = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return long.TryParse(strValue, out long value) ? value : 0;
            }
        }

        /// <summary>
        /// Sets the global upload speed limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetGlobalUploadLimitAsync(
                    long limit,
                    CancellationToken token = default)
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setGlobalUpLimit");
                var response = await _client.PostAsync(uri, BuildForm(("limit", limit.ToString())), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        #endregion

        #region Priority

        /// <summary>
        /// Changes the torrent priority.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ChangeTorrentPriorityAsync(
                    [NotNull] string hash,
                    TorrentPriorityChange change,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ChangeTorrentPriorityAsync(new[] { hash }, change, token);
        }

        /// <summary>
        /// Changes the torrent priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="change">The priority change.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ChangeTorrentPriorityAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    TorrentPriorityChange change,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            var path = GetPath();
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri(path);
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(("hashes", hashesString)),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            string GetPath()
            {
                switch (change)
                {
                    case TorrentPriorityChange.Minimal:
                        return "/command/bottomPrio";
                    case TorrentPriorityChange.Increase:
                        return "/command/decreasePrio";
                    case TorrentPriorityChange.Decrease:
                        return "/command/increasePrio";
                    case TorrentPriorityChange.Maximal:
                        return "/command/topPrio";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(change), change, null);
                }
            }
        }

        /// <summary>
        /// Sets the file priority.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="fileId">The file identifier.</param>
        /// <param name="priority">The priority.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetFilePriorityAsync(
                    [NotNull] string hash,
                    int fileId,
                    TorrentContentPriority priority,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            if (fileId < 0)
                throw new ArgumentOutOfRangeException(nameof(fileId));
            if (!Enum.GetValues(typeof(TorrentContentPriority)).Cast<TorrentContentPriority>().Contains(priority))
                throw new ArgumentOutOfRangeException(nameof(priority));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setFilePrio");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(
                            ("hash", hash),
                            ("id", fileId.ToString()),
                            ("priority", priority.ToString("D"))),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        #endregion

        #region Other

        /// <summary>
        /// Deletes the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteAsync(
                    [NotNull] string hash,
                    bool deleteDownloadedData = false,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = deleteDownloadedData
                    ? BuildUri("/command/deletePerm")
                    : BuildUri("/command/delete");
                var response = await _client.PostAsync(uri, BuildForm(("hashes", hash)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Deletes the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="deleteDownloadedData"><see langword="true"/> to delete the downloaded data.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task DeleteAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    bool deleteDownloadedData = false,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = deleteDownloadedData
                    ? BuildUri("/command/deletePerm")
                    : BuildUri("/command/delete");
                var response = await _client.PostAsync(
                        uri,
                        BuildForm(("hashes", hashesString)),
                        token)
                    .ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Sets the location of torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetLocationAsync(
                    [NotNull] string hash,
                    [NotNull] string newLocation,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            if (newLocation == null)
                throw new ArgumentNullException(nameof(newLocation));
            if (string.IsNullOrEmpty(newLocation))
                throw new ArgumentException("The location cannot be an empty string.", nameof(newLocation));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setLocation");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hash),
                        ("location", newLocation)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Sets the location of the torrents.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="newLocation">The new location.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetLocationAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    [NotNull] string newLocation,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            if (newLocation == null)
                throw new ArgumentNullException(nameof(newLocation));
            if (string.IsNullOrEmpty(newLocation))
                throw new ArgumentException("The location cannot be an empty string.", nameof(newLocation));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setLocation");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hashesString),
                        ("location", newLocation)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Renames the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="newName">The new name.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task RenameAsync(
                    [NotNull] string hash,
                    [NotNull] string newName,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            if (newName == null)
                throw new ArgumentNullException(nameof(newName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("The name cannot be an empty string.", nameof(newName));

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/rename");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hash", hash),
                        ("name", newName)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Adds the tracker to the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="tracker">The tracker.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTrackerAsync(
                    [NotNull] string hash,
                    [NotNull] Uri tracker,
                    CancellationToken token = default)
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (!tracker.IsAbsoluteUri)
                throw new ArgumentException("The URI must be absolute.", nameof(tracker));

            return AddTrackersAsync(hash, new[] { tracker }, token);
        }

        /// <summary>
        /// Adds the trackers to the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="trackers">The trackers.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task AddTrackersAsync(
                    [NotNull] string hash,
                    [NotNull, ItemNotNull] IEnumerable<Uri> trackers,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            var urls = GetUrls();

            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/addTrackers");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hash", hash),
                        ("urls", urls)
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }

            string GetUrls()
            {
                if (trackers == null)
                    throw new ArgumentNullException(nameof(trackers));

                var builder = new StringBuilder(4096);
                foreach (var tracker in trackers)
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (tracker == null)
                        throw new ArgumentException("The collection must not contain nulls.", nameof(trackers));
                    if (!tracker.IsAbsoluteUri)
                        throw new ArgumentException("The collection must contain absolute URIs.", nameof(trackers));

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(tracker.AbsoluteUri);
                }

                if (builder.Length == 0)
                    throw new ArgumentException("The collection must contain at least one URI.", nameof(trackers));

                return builder.ToString();
            }
        }

        /// <summary>
        /// Rechecks the torrent.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task RecheckAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/recheck");
                var response = await _client.PostAsync(uri, BuildForm(("hash", hash)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Gets the server log.
        /// </summary>
        /// <param name="severity">The severity of log entries to return. <see cref="TorrentLogSeverity.All"/> by default.</param>
        /// <param name="afterId">Return the entries with the ID greater than the specified one.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<IEnumerable<TorrentLogEntry>> GetLogAsync(
                    TorrentLogSeverity severity = TorrentLogSeverity.All,
                    int afterId = -1,
                    CancellationToken token = default)
        {
            var uri = BuildUri("/query/getLog",
                ("normal", severity.HasFlag(TorrentLogSeverity.Normal).ToLowerString()),
                ("info", severity.HasFlag(TorrentLogSeverity.Info).ToLowerString()),
                ("warning", severity.HasFlag(TorrentLogSeverity.Warning).ToLowerString()),
                ("critical", severity.HasFlag(TorrentLogSeverity.Critical).ToLowerString()),
                ("last_known_id", afterId.ToString())
            );

            var json = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IEnumerable<TorrentLogEntry>>(json);
        }

        /// <summary>
        /// Gets the value indicating whether the alternative speed limits are enabled.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task<bool> GetAlternativeSpeedLimitsEnabledAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/alternativeSpeedLimitsEnabled");
            var result = await _client.GetStringAsync(uri, token).ConfigureAwait(false);
            return result == "1";
        }

        /// <summary>
        /// Toggles the alternative speed limits.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public async Task ToggleAlternativeSpeedLimitsAsync(
                    CancellationToken token = default)
        {
            var uri = BuildUri("/command/toggleAlternativeSpeedLimits");
            using (var result = await _client.PostAsync(uri, BuildForm(), token).ConfigureAwait(false))
            {
                result.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Sets the automatic torrent management.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetAutomaticTorrentManagementAsync(
                    [NotNull] string hash,
                    bool enabled,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return SetAutomaticTorrentManagementAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Sets the automatic torrent management.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetAutomaticTorrentManagementAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    bool enabled,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setAutoTMM");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hashesString),
                        ("enable", enabled.ToLowerString())
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Sets the force start.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetForceStartAsync(
                    [NotNull] string hash,
                    bool enabled,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return SetForceStartAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Sets the force start.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetForceStartAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    bool enabled,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setForceStart");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hashesString),
                        ("value", enabled.ToLowerString())
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Sets the super seeding.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetSuperSeedingAsync(
                    [NotNull] string hash,
                    bool enabled,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return SetSuperSeedingAsync(new[] { hash }, enabled, token);
        }

        /// <summary>
        /// Sets the super seeding asynchronous.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="enabled"></param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task SetSuperSeedingAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    bool enabled,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/setSuperSeeding");
                var response = await _client.PostAsync(uri,
                    BuildForm(
                        ("hashes", hashesString),
                        ("value", enabled.ToLowerString())
                    ), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Toggles the first and last piece priority.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleFirstLastPiecePrioritizedAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ToggleFirstLastPiecePrioritizedAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Toggles the first and last piece priority.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleFirstLastPiecePrioritizedAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/toggleFirstLastPiecePrio");
                var response = await _client.PostAsync(uri,
                    BuildForm(("hashes", hashesString)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// Toggles the sequential download.
        /// </summary>
        /// <param name="hash">The torrent hash.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleSequentialDownloadAsync(
                    [NotNull] string hash,
                    CancellationToken token = default)
        {
            ValidateHash(hash);
            return ToggleSequentialDownloadAsync(new[] { hash }, token);
        }

        /// <summary>
        /// Toggles the sequential download.
        /// </summary>
        /// <param name="hashes">The torrent hashes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task ToggleSequentialDownloadAsync(
                    [NotNull, ItemNotNull] IEnumerable<string> hashes,
                    CancellationToken token = default)
        {
            var hashesString = JoinHashes(hashes);
            return ExecuteAsync();

            async Task ExecuteAsync()
            {
                var uri = BuildUri("/command/toggleSequentialDownload");
                var response = await _client.PostAsync(uri,
                    BuildForm(("hashes", hashesString)), token).ConfigureAwait(false);
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            _client?.Dispose();
        }

        private HttpContent BuildForm(params (string key, string value)[] fields)
        {
            return new CompatibleFormUrlEncodedContent(fields);
        }

        private Uri BuildUri(string path, params (string key, string value)[] parameters)
        {
            var builder = new UriBuilder(_uri)
            {
                Path = path,
                Query = string.Join("&", parameters
                    .Where(t => t.value != null)
                    .Select(t => $"{Uri.EscapeDataString(t.key)}={Uri.EscapeDataString(t.value)}"))
            };
            return builder.Uri;
        }

        private struct UrlItem
        {
            [JsonProperty("url")]
            public Uri Url { get; set; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HashIsValid(string hash)
        {
            return hash.Length == 40 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        private static void ValidateHash(string hash)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            if (!HashIsValid(hash))
                throw new ArgumentException("The parameter must be a hexadecimal representation of SHA-1 hash.", nameof(hash));
        }

        [ContractAnnotation("null => halt")]
        private static string JoinHashes(IEnumerable<string> hashes)
        {
            if (hashes == null)
                throw new ArgumentNullException(nameof(hashes));

            var builder = new StringBuilder(4096);
            foreach (var hash in hashes)
            {
                if (hash == null || !HashIsValid(hash))
                    throw new ArgumentException("The values must be hexadecimal representations of SHA-1 hashes.", nameof(hashes));

                if (builder.Length > 0)
                {
                    builder.Append('|');
                }

                builder.Append(hash);
            }

            if (builder.Length == 0)
                throw new ArgumentException("The list of hashes cannot be empty.", nameof(hashes));

            return builder.ToString();
        }
    }
}