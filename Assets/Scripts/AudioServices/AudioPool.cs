using System;
using UnityEngine;
using UnityEngine.Pool;


/// <summary>
/// ������Ʈ Ǯ�� ����� <see cref="AudioSource">AudioSource</see>�� ����, �Ҹ��� �����մϴ�.
/// </summary>
public interface IAudioPool
{
    /// <summary>
    /// ��� ��ġ���� ������ �������� ���� �� �ִ� ������� ����մϴ�.
    /// </summary>
    /// <param name="onEnd"><see cref="AudioSource.isPlaying">AudioSource.isPlaying</see>�� false�� ���� �� ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayGlobalAudio(AudioClip clip, Action onEnd = null);


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ��ǥ ��ġ�� �����ϰ� ����մϴ�.
    /// </summary>
    /// <param name="onEnd"><see cref="AudioSource.isPlaying">AudioSource.isPlaying</see>�� false�� ���� �� ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayLocalAudio(AudioClip clip, Vector3 position, Action onEnd = null);


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ��ǥ ����� �θ�� �����ϰ� ����մϴ�.
    /// </summary>
    /// <param name="onEnd"><see cref="AudioSource.isPlaying">AudioSource.isPlaying</see>�� false�� ���� �� ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayLocalAudio(AudioClip clip, Transform parent, Action onEnd = null);
}


/// <summary>
/// ������Ʈ Ǯ�� ����� <see cref="AudioSource">AudioSource</see>�� ����, �Ҹ��� �����մϴ�.
/// </summary>
public class AudioPool : IAudioPool
{
    #region Nested Class, Struct

    public struct Settings
    {
        public int globalAudioPoolDefaultSize, globalAudioPoolMaxSize;
        public int localAudioPoolDefaultSize, localAudioPoolMaxSize;

        public float globalAudioReverbZoneMix, localAudioReverbZoneMix;
    }

    private class AudioElement : IDisposable
    {
        public readonly AudioSource source;
        private readonly IObjectPool<AudioElement> pool;


        public AudioElement(AudioSource source, IObjectPool<AudioElement> pool)
        {
            this.source = source;
            this.pool = pool;
        }

        public void Dispose()
        {
            pool.Release(this);
        }
    }

    #endregion


    #region Members

    private readonly Settings settings;
    private readonly Transform globalAudioPoolRoot, localAudioPoolRoot;

    private readonly ObjectPool<AudioElement> globalAudioPool, localAudioPool;

    #endregion


    public AudioPool(Settings settings)
    {
        this.settings = settings;
        this.globalAudioPoolRoot = new GameObject("GlobalAudioPool Root").transform;

        this.globalAudioPool = new ObjectPool<AudioElement>(createFunc: CreateGlobalAudioElement,
                                                       actionOnGet: OnTakeFromPool,
                                                       actionOnRelease: OnReturnedToPool,
                                                       actionOnDestroy: OnDestroyObject,
                                                       collectionCheck: true,
                                                       defaultCapacity: settings.globalAudioPoolDefaultSize,
                                                       maxSize: settings.globalAudioPoolMaxSize);

        this.localAudioPool = new ObjectPool<AudioElement>(createFunc: CreateLocalAudioElement,
                                               actionOnGet: OnTakeFromPool,
                                               actionOnRelease: OnReturnedToPool,
                                               actionOnDestroy: OnDestroyObject,
                                               collectionCheck: true,
                                               defaultCapacity: settings.localAudioPoolDefaultSize,
                                               maxSize: settings.localAudioPoolMaxSize);
    }


    public IDisposable PlayGlobalAudio(AudioClip clip, Action onEnd = null)
    {
        var newElement = CreateGlobalAudioElement();
        newElement.source.PlayOneShot(clip);

        return newElement;
    }


    public IDisposable PlayLocalAudio(AudioClip clip, Vector3 position, Action onEnd = null)
    {
        var newElement = PlayLocalAudio(clip, onEnd);
        newElement.source.transform.position = position;

        return newElement;
    }


    public IDisposable PlayLocalAudio(AudioClip clip, Transform parent, Action onEnd = null)
    {
        var newElement = PlayLocalAudio(clip, onEnd);
        newElement.source.transform.SetParent(parent);
        newElement.source.transform.localPosition = Vector3.zero;

        return newElement;
    }


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ����մϴ�.
    /// </summary>
    /// <param name="onEnd"><see cref="AudioSource.isPlaying">AudioSource.isPlaying</see>�� false�� ���� �� ȣ��˴ϴ�.</param>
    private AudioElement PlayLocalAudio(AudioClip clip, Action onEnd = null)
    {
        var newElement = CreateLocalAudioElement();
        newElement.source.PlayOneShot(clip);

        return newElement;
    }


    #region Pool Manage Functions

    private void OnDestroyObject(AudioElement element)
    {
        UnityEngine.Object.Destroy(element.source.gameObject);
    }


    private void OnReturnedToPool(AudioElement element)
    {
        element.source.gameObject.SetActive(false);
        element.source.transform.SetParent(globalAudioPoolRoot);
    }


    private void OnTakeFromPool(AudioElement element)
    {
        element.source.gameObject.SetActive(true);
    }


    private AudioElement CreateGlobalAudioElement()
    {
        GameObject newObject = new GameObject("GlobalAudioPool Element");
        AudioSource source = newObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.reverbZoneMix = settings.globalAudioReverbZoneMix;
        newObject.transform.SetParent(globalAudioPoolRoot);

        AudioElement newElement = new AudioElement(source, globalAudioPool);

        return newElement;
    }


    private AudioElement CreateLocalAudioElement()
    {
        GameObject newObject = new GameObject("GlobalAudioPool Element");
        AudioSource source = newObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.reverbZoneMix = settings.localAudioReverbZoneMix;
        newObject.transform.SetParent(localAudioPoolRoot);

        AudioElement newElement = new AudioElement(source, localAudioPool);

        return newElement;
    }

    #endregion

}