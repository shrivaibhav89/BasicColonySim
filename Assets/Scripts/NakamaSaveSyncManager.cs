using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-2200)]
public class NakamaSaveSyncManager : MonoBehaviour
{
    private const string DeviceIdPrefKey = "nakama.deviceId";
    private const string AuthTokenPrefKey = "nakama.authToken";
    private const string RefreshTokenPrefKey = "nakama.refreshToken";
    private const string UserIdPrefKey = "nakama.userId";
    private const string VillageKeyPrefKey = "nakama.villageKey";

    private static readonly char[] VillageKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    private static NakamaSaveSyncManager instance;

    [Header("Nakama Server")]
    [SerializeField] private string scheme = "http";
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 7350;
    [SerializeField] private string serverKey = "colony_dev_server_key";

    [Header("Private Save Object")]
    [SerializeField] private string storageCollection = "colony";
    [SerializeField] private string storageKey = "savegame";
    [SerializeField] private int permissionRead = 1;
    [SerializeField] private int permissionWrite = 1;

    [Header("Public Village Object")]
    [SerializeField] private string publicVillageCollection = "colony_village";
    [SerializeField] private string publicVillageKey = "snapshot";

    [Header("Retry")]
    [SerializeField] private float authRetrySeconds = 10f;
    [SerializeField] private float uploadRetrySeconds = 5f;
    [SerializeField] private int requestTimeoutSeconds = 15;

    private bool isAuthenticated;
    private string authToken = string.Empty;
    private string userId = string.Empty;
    private string accountUsername = string.Empty;
    private string villageKey = string.Empty;

    private Coroutine authRoutine;
    private Coroutine uploadRoutine;
    private PendingCloudSave pendingSave;

    public static event Action<string> VillageKeyChanged;

    public static string CurrentVillageKey
    {
        get
        {
            if (instance != null)
            {
                return instance.villageKey;
            }

            return PlayerPrefs.GetString(VillageKeyPrefKey, string.Empty);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<NakamaSaveSyncManager>() != null)
        {
            return;
        }

        GameObject go = new GameObject("NakamaSaveSyncManager");
        instance = go.AddComponent<NakamaSaveSyncManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        authToken = PlayerPrefs.GetString(AuthTokenPrefKey, string.Empty);
        userId = PlayerPrefs.GetString(UserIdPrefKey, string.Empty);
        villageKey = PlayerPrefs.GetString(VillageKeyPrefKey, string.Empty);
    }

    private void OnEnable()
    {
        SaveLoadManager.SaveCompleted += HandleLocalSaveCompleted;
        SaveLoadManager.CloudLoadProvider = LoadCloudSaveData;
        SaveLoadManager.CloudVillageLoadProvider = LoadVillageSaveByKey;
    }

    private void OnDisable()
    {
        SaveLoadManager.SaveCompleted -= HandleLocalSaveCompleted;

        if (SaveLoadManager.CloudLoadProvider == LoadCloudSaveData)
        {
            SaveLoadManager.CloudLoadProvider = null;
        }

        if (SaveLoadManager.CloudVillageLoadProvider == LoadVillageSaveByKey)
        {
            SaveLoadManager.CloudVillageLoadProvider = null;
        }
    }

    private void Start()
    {
        EnsureAuthLoopRunning();
    }

    private void EnsureAuthLoopRunning()
    {
        if (authRoutine == null)
        {
            authRoutine = StartCoroutine(AuthenticateLoop());
        }
    }

    private IEnumerator AuthenticateLoop()
    {
        while (!isAuthenticated)
        {
            yield return StartCoroutine(AuthenticateWithDeviceOnce());

            if (!isAuthenticated)
            {
                yield return new WaitForSeconds(authRetrySeconds);
            }
        }

        authRoutine = null;
        TryUploadPending();
    }

    private IEnumerator AuthenticateWithDeviceOnce()
    {
        string deviceId = GetOrCreateDeviceId();
        DeviceAuthRequest authRequest = new DeviceAuthRequest { id = deviceId };
        string requestJson = JsonUtility.ToJson(authRequest);
        string url = GetBaseUrl() + "/v2/account/authenticate/device?create=true";

        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPOST, requestJson))
        {
            request.SetRequestHeader("Authorization", BuildBasicAuthHeader(serverKey));
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                Debug.LogWarning("Nakama auth failed: " + GetRequestError(request));
                yield break;
            }

            DeviceAuthResponse response = JsonUtility.FromJson<DeviceAuthResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrEmpty(response.token))
            {
                Debug.LogWarning("Nakama auth failed: empty token.");
                yield break;
            }

            authToken = response.token;
            PlayerPrefs.SetString(AuthTokenPrefKey, authToken);
            PlayerPrefs.SetString(RefreshTokenPrefKey, response.refresh_token ?? string.Empty);

            bool accountResolved = false;
            yield return StartCoroutine(FetchAccountOnce(success => accountResolved = success));
            if (!accountResolved || string.IsNullOrEmpty(userId))
            {
                isAuthenticated = false;
                Debug.LogWarning("Nakama auth failed: could not resolve account.");
                yield break;
            }

            bool keyReady = false;
            yield return StartCoroutine(EnsureVillageKeyOnce(success => keyReady = success));
            if (!keyReady)
            {
                Debug.LogWarning("Village key assignment failed. Public village loading may be unavailable.");
            }

            isAuthenticated = true;
            PlayerPrefs.Save();
            Debug.Log("Nakama auth success (device).");
        }
    }

    private IEnumerator FetchAccountOnce(Action<bool> onComplete)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        string url = GetBaseUrl() + "/v2/account";
        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbGET, null))
        {
            request.SetRequestHeader("Authorization", "Bearer " + authToken);
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                Debug.LogWarning("Nakama account fetch failed: " + GetRequestError(request));
                onComplete?.Invoke(false);
                yield break;
            }

            AccountResponse account = JsonUtility.FromJson<AccountResponse>(request.downloadHandler.text);
            if (account == null || account.user == null || string.IsNullOrEmpty(account.user.id))
            {
                onComplete?.Invoke(false);
                yield break;
            }

            userId = account.user.id;
            accountUsername = account.user.username ?? string.Empty;

            PlayerPrefs.SetString(UserIdPrefKey, userId);
            PlayerPrefs.Save();
            onComplete?.Invoke(true);
        }
    }

    private IEnumerator EnsureVillageKeyOnce(Action<bool> onComplete)
    {
        if (SaveLoadManager.IsValidVillageKey(accountUsername))
        {
            SetVillageKey(accountUsername);
            onComplete?.Invoke(true);
            yield break;
        }

        const int maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++)
        {
            string candidate = GenerateVillageKey();
            bool updated = false;
            int responseCode = 0;
            string updateError = null;

            yield return StartCoroutine(UpdateUsernameOnce(candidate, (success, code, error) =>
            {
                updated = success;
                responseCode = code;
                updateError = error;
            }));

            if (updated)
            {
                accountUsername = candidate;
                SetVillageKey(candidate);
                onComplete?.Invoke(true);
                yield break;
            }

            if (responseCode != 409 && !IsUsernameTakenError(updateError))
            {
                Debug.LogWarning("Village key update attempt failed: " + updateError);
            }
        }

        onComplete?.Invoke(false);
    }

    private IEnumerator UpdateUsernameOnce(string username, Action<bool, int, string> onComplete)
    {
        if (!SaveLoadManager.IsValidVillageKey(username))
        {
            onComplete?.Invoke(false, 0, "Invalid village key username.");
            yield break;
        }

        AccountUpdateRequest requestBody = new AccountUpdateRequest { username = username };
        string requestJson = JsonUtility.ToJson(requestBody);
        string url = GetBaseUrl() + "/v2/account";

        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPUT, requestJson))
        {
            request.SetRequestHeader("Authorization", "Bearer " + authToken);
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                onComplete?.Invoke(false, (int)request.responseCode, GetRequestError(request));
                yield break;
            }

            onComplete?.Invoke(true, (int)request.responseCode, null);
        }
    }

    private void SetVillageKey(string key)
    {
        string normalized = SaveLoadManager.NormalizeVillageKey(key);
        if (!SaveLoadManager.IsValidVillageKey(normalized))
        {
            return;
        }

        villageKey = normalized;
        PlayerPrefs.SetString(VillageKeyPrefKey, villageKey);
        PlayerPrefs.Save();
        VillageKeyChanged?.Invoke(villageKey);
        Debug.Log("Village key ready: " + villageKey);
    }

    private static bool IsUsernameTakenError(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        string lower = error.ToLowerInvariant();
        return lower.Contains("already") && lower.Contains("username");
    }

    private static string GenerateVillageKey()
    {
        byte[] bytes = new byte[6];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        char[] chars = new char[6];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = VillageKeyChars[bytes[i] % VillageKeyChars.Length];
        }

        return new string(chars);
    }

    private void HandleLocalSaveCompleted(ColonySaveData data, string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
        {
            return;
        }

        PendingCloudSave nextSave = new PendingCloudSave();
        nextSave.saveVersion = data != null ? data.version : 1;
        nextSave.savedAtUtc = data != null ? data.savedAtUtc : DateTime.UtcNow.ToString("o");
        nextSave.sceneName = data != null ? data.sceneName : string.Empty;
        nextSave.encryptedPayloadBase64 = encryptedBase64;

        pendingSave = nextSave;

        if (SaveLoadManager.IsValidVillageKey(villageKey))
        {
            Debug.Log("Village key: " + villageKey);
        }
        else
        {
            Debug.Log("Village key not ready yet. Waiting for Nakama auth/key assignment.");
        }

        EnsureAuthLoopRunning();
        TryUploadPending();
    }

    private void TryUploadPending()
    {
        if (uploadRoutine == null)
        {
            uploadRoutine = StartCoroutine(UploadPendingLoop());
        }
    }

    private IEnumerator UploadPendingLoop()
    {
        while (pendingSave != null)
        {
            if (!isAuthenticated)
            {
                EnsureAuthLoopRunning();
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            PendingCloudSave current = pendingSave;
            pendingSave = null;

            bool uploadSuccess = false;
            yield return StartCoroutine(UploadSingleSave(current, success => uploadSuccess = success));

            if (!uploadSuccess)
            {
                pendingSave = current;
                isAuthenticated = false;
                EnsureAuthLoopRunning();
                yield return new WaitForSeconds(uploadRetrySeconds);
            }
        }

        uploadRoutine = null;
    }

    private IEnumerator UploadSingleSave(PendingCloudSave save, Action<bool> onComplete)
    {
        if (save == null || string.IsNullOrEmpty(authToken))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        CloudSaveEnvelope envelope = new CloudSaveEnvelope();
        envelope.saveVersion = save.saveVersion;
        envelope.savedAtUtc = save.savedAtUtc;
        envelope.sceneName = save.sceneName;
        envelope.villageKey = villageKey;
        envelope.encryptedPayloadBase64 = save.encryptedPayloadBase64;

        string envelopeJson = JsonUtility.ToJson(envelope);
        List<StorageWriteObject> objects = new List<StorageWriteObject>();

        StorageWriteObject privateSaveObject = new StorageWriteObject();
        privateSaveObject.collection = storageCollection;
        privateSaveObject.key = storageKey;
        privateSaveObject.value = envelopeJson;
        privateSaveObject.permission_read = permissionRead;
        privateSaveObject.permission_write = permissionWrite;
        objects.Add(privateSaveObject);

        if (SaveLoadManager.IsValidVillageKey(villageKey))
        {
            StorageWriteObject publicVillageObject = new StorageWriteObject();
            publicVillageObject.collection = publicVillageCollection;
            publicVillageObject.key = publicVillageKey;
            publicVillageObject.value = envelopeJson;
            publicVillageObject.permission_read = 2;
            publicVillageObject.permission_write = 1;
            objects.Add(publicVillageObject);
        }

        StorageWriteRequest writeRequest = new StorageWriteRequest();
        writeRequest.objects = objects.ToArray();

        string requestJson = JsonUtility.ToJson(writeRequest);
        string url = GetBaseUrl() + "/v2/storage";

        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPUT, requestJson))
        {
            request.SetRequestHeader("Authorization", "Bearer " + authToken);
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                Debug.LogWarning("Nakama cloud save failed: " + GetRequestError(request));
                onComplete?.Invoke(false);
                yield break;
            }

            if (SaveLoadManager.IsValidVillageKey(villageKey))
            {
                Debug.Log("Nakama cloud save synced. Village key: " + villageKey);
            }
            else
            {
                Debug.Log("Nakama cloud save synced.");
            }
            onComplete?.Invoke(true);
        }
    }

    private IEnumerator LoadCloudSaveData(Action<ColonySaveData> onLoaded, Action<string> onError)
    {
        if (!isAuthenticated)
        {
            yield return StartCoroutine(AuthenticateWithDeviceOnce());
        }

        if (!isAuthenticated || string.IsNullOrEmpty(authToken))
        {
            onError?.Invoke("Not authenticated to Nakama.");
            yield break;
        }

        if (string.IsNullOrEmpty(userId))
        {
            bool gotAccount = false;
            yield return StartCoroutine(FetchAccountOnce(success => gotAccount = success));
            if (!gotAccount || string.IsNullOrEmpty(userId))
            {
                onError?.Invoke("Could not resolve Nakama user id.");
                yield break;
            }
        }

        yield return StartCoroutine(ReadSaveForUser(storageCollection, storageKey, userId, onLoaded, onError));
    }

    private IEnumerator LoadVillageSaveByKey(string requestedVillageKey, Action<ColonySaveData> onLoaded, Action<string> onError)
    {
        string normalizedKey = SaveLoadManager.NormalizeVillageKey(requestedVillageKey);
        if (!SaveLoadManager.IsValidVillageKey(normalizedKey))
        {
            onError?.Invoke("Village key must be 6 chars (A-Z, 0-9).");
            yield break;
        }

        if (!isAuthenticated)
        {
            yield return StartCoroutine(AuthenticateWithDeviceOnce());
        }

        if (!isAuthenticated || string.IsNullOrEmpty(authToken))
        {
            onError?.Invoke("Not authenticated to Nakama.");
            yield break;
        }

        string targetUserId = null;
        string lookupError = null;
        yield return StartCoroutine(LookupUserIdByVillageKey(normalizedKey, id => targetUserId = id, error => lookupError = error));

        if (!string.IsNullOrEmpty(lookupError))
        {
            onError?.Invoke(lookupError);
            yield break;
        }

        if (string.IsNullOrEmpty(targetUserId))
        {
            onError?.Invoke("Village key not found.");
            yield break;
        }

        yield return StartCoroutine(ReadSaveForUser(publicVillageCollection, publicVillageKey, targetUserId, onLoaded, onError));
    }

    private IEnumerator LookupUserIdByVillageKey(string normalizedVillageKey, Action<string> onResolved, Action<string> onError)
    {
        string encoded = UnityWebRequest.EscapeURL(normalizedVillageKey);
        string url = GetBaseUrl() + "/v2/user?usernames=" + encoded;

        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbGET, null))
        {
            request.SetRequestHeader("Authorization", "Bearer " + authToken);
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                onError?.Invoke("Village lookup failed: " + GetRequestError(request));
                yield break;
            }

            UsersLookupResponse response = JsonUtility.FromJson<UsersLookupResponse>(request.downloadHandler.text);
            if (response == null || response.users == null || response.users.Length == 0)
            {
                onError?.Invoke("Village key not found.");
                yield break;
            }

            if (response.users[0] == null || string.IsNullOrEmpty(response.users[0].id))
            {
                onError?.Invoke("Village lookup returned invalid user.");
                yield break;
            }

            onResolved?.Invoke(response.users[0].id);
        }
    }

    private IEnumerator ReadSaveForUser(string collection, string key, string targetUserId, Action<ColonySaveData> onLoaded, Action<string> onError)
    {
        StorageReadRequest readRequest = new StorageReadRequest();
        readRequest.object_ids = new[] {
            new StorageObjectIdRequest
            {
                collection = collection,
                key = key,
                user_id = targetUserId
            }
        };

        string readJson = JsonUtility.ToJson(readRequest);
        string url = GetBaseUrl() + "/v2/storage";

        using (UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPOST, readJson))
        {
            request.SetRequestHeader("Authorization", "Bearer " + authToken);
            yield return request.SendWebRequest();

            if (!IsRequestSuccessful(request))
            {
                onError?.Invoke(GetRequestError(request));
                yield break;
            }

            StorageReadResponse response = JsonUtility.FromJson<StorageReadResponse>(request.downloadHandler.text);
            if (response == null || response.objects == null || response.objects.Length == 0)
            {
                onError?.Invoke("No cloud save found.");
                yield break;
            }

            string value = response.objects[0] != null ? response.objects[0].value : null;
            if (string.IsNullOrEmpty(value))
            {
                onError?.Invoke("Cloud save payload is empty.");
                yield break;
            }

            CloudSaveEnvelope envelope = JsonUtility.FromJson<CloudSaveEnvelope>(value);
            if (envelope == null || string.IsNullOrEmpty(envelope.encryptedPayloadBase64))
            {
                onError?.Invoke("Cloud save payload format is invalid.");
                yield break;
            }

            ColonySaveData decoded;
            string decodeError;
            if (!SaveLoadManager.TryDecodeSaveDataFromEncryptedBase64(envelope.encryptedPayloadBase64, out decoded, out decodeError))
            {
                onError?.Invoke("Cloud save decrypt failed: " + decodeError);
                yield break;
            }

            onLoaded?.Invoke(decoded);
        }
    }

    private UnityWebRequest CreateJsonRequest(string url, string method, string bodyJson)
    {
        UnityWebRequest request = new UnityWebRequest(url, method);
        if (!string.IsNullOrEmpty(bodyJson))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        }

        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = requestTimeoutSeconds;
        request.SetRequestHeader("Accept", "application/json");

        if (!string.IsNullOrEmpty(bodyJson))
        {
            request.SetRequestHeader("Content-Type", "application/json");
        }

        return request;
    }

    private string GetBaseUrl()
    {
        return string.Format("{0}://{1}:{2}", scheme, host, port);
    }

    private string GetOrCreateDeviceId()
    {
        string deviceId = PlayerPrefs.GetString(DeviceIdPrefKey, SystemInfo.deviceUniqueIdentifier);
        if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
        {
            deviceId = Guid.NewGuid().ToString("N");
        }

        PlayerPrefs.SetString(DeviceIdPrefKey, deviceId);
        PlayerPrefs.Save();
        return deviceId;
    }

    private static string BuildBasicAuthHeader(string key)
    {
        string raw = (key ?? string.Empty) + ":";
        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return "Basic " + token;
    }

    private static bool IsRequestSuccessful(UnityWebRequest request)
    {
#if UNITY_2020_2_OR_NEWER
        return request.result == UnityWebRequest.Result.Success;
#else
        return !request.isNetworkError && !request.isHttpError;
#endif
    }

    private static string GetRequestError(UnityWebRequest request)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        return string.Format("HTTP {0}: {1}. Body: {2}", request.responseCode, request.error, body);
    }

    [Serializable]
    private class PendingCloudSave
    {
        public int saveVersion;
        public string savedAtUtc;
        public string sceneName;
        public string encryptedPayloadBase64;
    }

    [Serializable]
    private class CloudSaveEnvelope
    {
        public int saveVersion;
        public string savedAtUtc;
        public string sceneName;
        public string villageKey;
        public string encryptedPayloadBase64;
    }

    [Serializable]
    private class DeviceAuthRequest
    {
        public string id;
    }

    [Serializable]
    private class DeviceAuthResponse
    {
        public string token;
        public string refresh_token;
    }

    [Serializable]
    private class AccountResponse
    {
        public AccountUser user;
    }

    [Serializable]
    private class AccountUser
    {
        public string id;
        public string username;
    }

    [Serializable]
    private class AccountUpdateRequest
    {
        public string username;
    }

    [Serializable]
    private class UsersLookupResponse
    {
        public LookupUser[] users;
    }

    [Serializable]
    private class LookupUser
    {
        public string id;
        public string username;
    }

    [Serializable]
    private class StorageWriteRequest
    {
        public StorageWriteObject[] objects;
    }

    [Serializable]
    private class StorageWriteObject
    {
        public string collection;
        public string key;
        public string value;
        public int permission_read;
        public int permission_write;
    }

    [Serializable]
    private class StorageReadRequest
    {
        public StorageObjectIdRequest[] object_ids;
    }

    [Serializable]
    private class StorageObjectIdRequest
    {
        public string collection;
        public string key;
        public string user_id;
    }

    [Serializable]
    private class StorageReadResponse
    {
        public StorageReadObject[] objects;
    }

    [Serializable]
    private class StorageReadObject
    {
        public string value;
    }
}
