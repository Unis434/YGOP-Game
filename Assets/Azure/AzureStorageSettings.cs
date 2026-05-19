using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AzureStorageSettings", menuName = "AzureStorageSettings")]
public sealed class AzureStorageSettings : ScriptableObject
{
    public string storageAccount;
    public string accessKey;
    public string bundleContainer;
}
