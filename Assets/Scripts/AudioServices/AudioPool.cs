using System;
using System.Collections;
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
    /// <param name="autoRemove">������� ������� ������ �ڵ����� �Ҹ�� �� �����մϴ�.</param>
    /// <param name="onEnd">������� ������� ������ ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayGlobalAudio(AudioClip clip, bool autoRemove = true, Action onEnd = null);


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ��ǥ ��ġ�� �����ϰ� ����մϴ�.
    /// </summary>
    /// <param name="autoRemove">������� ������� ������ �ڵ����� �Ҹ�� �� �����մϴ�.</param>
    /// <param name="onEnd">������� ������� ������ ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayLocalAudio(AudioClip clip, Vector3 position, bool autoRemove = true, Action onEnd = null);


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ��ǥ ����� �θ�� �����ϰ� ����մϴ�.
    /// </summary>
    /// <param name="autoRemove">������� ������� ������ �ڵ����� �Ҹ�� �� �����մϴ�.</param>
    /// <param name="onEnd">������� ������� ������ ȣ��˴ϴ�.</param>
    /// <returns>Pool�� ��ȯ�ϴ� Dispose �Լ��� �����ϴ� Disposable</returns>
    IDisposable PlayLocalAudio(AudioClip clip, Transform parent, bool autoRemove = true, Action onEnd = null);
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
    private readonly ICoroutineRunner coroutineRunner;

    private readonly ObjectPool<AudioElement> globalAudioPool, localAudioPool;

    #endregion


    public AudioPool(Settings settings, ICoroutineRunner coroutineRunner)
    {
        this.settings = settings;
        this.coroutineRunner = coroutineRunner;

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


    public IDisposable PlayGlobalAudio(AudioClip clip, bool autoRemove = true, Action onEnd = null)
    {
        var newElement = globalAudioPool.Get();
        newElement.source.clip = clip;
        newElement.source.Play();

        coroutineRunner.RequestStartCoroutine(AudioElementEndCheck(newElement, autoRemove, onEnd));

        return newElement;
    }


    public IDisposable PlayLocalAudio(AudioClip clip, Vector3 position, bool autoRemove = true, Action onEnd = null)
    {
        var newElement = PlayLocalAudio(clip, autoRemove, onEnd);
        newElement.source.transform.position = position;

        return newElement;
    }


    public IDisposable PlayLocalAudio(AudioClip clip, Transform parent, bool autoRemove = true, Action onEnd = null)
    {
        var newElement = PlayLocalAudio(clip, autoRemove, onEnd);
        newElement.source.transform.SetParent(parent);
        newElement.source.transform.localPosition = Vector3.zero;

        return newElement;
    }


    /// <summary>
    /// ��ġ�� ���� ������ �޶����� ������� ����մϴ�.
    /// </summary>
    /// <param name="onEnd"><see cref="AudioSource.isPlaying">AudioSource.isPlaying</see>�� false�� ���� �� ȣ��˴ϴ�.</param>
    private AudioElement PlayLocalAudio(AudioClip clip, bool autoRemove, Action onEnd)
    {
        var newElement = localAudioPool.Get();
        newElement.source.PlayOneShot(clip);

        coroutineRunner.RequestStartCoroutine(AudioElementEndCheck(newElement, autoRemove, onEnd));

        return newElement;
    }


    /// <summary>
    /// ������� ������� ������ element�� �Ҹ��Ű�� onEnd �̺�Ʈ�� �߻��ϴ� �ڷ�ƾ�� �����մϴ�.
    /// </summary>
    private IEnumerator AudioElementEndCheck(AudioElement element, bool autoRemove, Action onEnd)
    {
        // AudioSource�� ����� ������ ���
        yield return new WaitUntil(() => !element.source.isPlaying);

        if (autoRemove)
            element.Dispose();

        onEnd?.Invoke();
    }


    #region Pool Manage Functions

    private void OnDestroyObject(AudioElement element)
    {
        UnityEngine.Object.Destroy(element.source.gameObject);
    }


    private void OnReturnedToPool(AudioElement element)
    {
        element.source.Stop();
        element.source.clip = null;
        element.source.gameObject.SetActive(false);
        element.source.transform.SetParent(globalAudioPoolRoot);
    }


    private void OnTakeFromPool(AudioElement element)
    {
        element.source.gameObject.SetActive(true);
    }


    /// <summary>
    /// globalAudioPool�� ���ο� Element�� �����ϴ� �Լ��Դϴ�.
    /// <para>
    /// ���: ObjectPool���� ������ �۾��� �ƴ� Pool�� �ʱ�ȭ�ϴ� ���� ���Ǵ� �Լ��Դϴ�.
    /// ObjectPool���� ��� ������ ������Ʈ�� �������� <see cref="ObjectPool{T}.Get">ObjectPool.Get()</see> �Լ��� �̿��ϼ���.
    /// </para>
    /// </summary>
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


    /// <summary>
    /// localAudioPool�� ���ο� Element�� �����ϴ� �Լ��Դϴ�.
    /// <para>
    /// ���: ObjectPool���� ������ �۾��� �ƴ� Pool�� �ʱ�ȭ�ϴ� ���� ���Ǵ� �Լ��Դϴ�.
    /// ObjectPool���� ��� ������ ������Ʈ�� �������� <see cref="ObjectPool{T}.Get">ObjectPool.Get()</see> �Լ��� �̿��ϼ���.
    /// </para>
    /// </summary>
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