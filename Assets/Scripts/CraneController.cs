using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class CraneController : MonoBehaviour
{
    // Start is called before the first frame update
    float rotationSpeed = 60.0f;
    float extensionSpeed = 10.0f;
    float ropeMinScale = 4.0f;
    float ropeMaxScale = 30.0f;
    float armMinScale = 10.0f;
    float armMaxScale = 30.0f;
    float containerSize = 5.0f;

    [SerializeField]
    Transform arm, head, hook, rope, container;
    bool isSelected = false;
    bool hasHit = false;
    bool hasContainer = false;
    bool isHookOverContainer = false;
    Vector3 _destPos;
    
    public enum CraneState
    {
        Dropping,
        Raising,
        Moving,
        Stopped,
    }
    CraneState _state;

    void Start()
    {
        _destPos = new Vector3(hook.position.x, 0, hook.position.z);
        _state = CraneState.Stopped;
    }

    void Extrude(Transform trf, float extrusion, float extrusionSpeed, Vector3 dir, float minScale, float maxScale)
    {
        Vector3 tmp = Vector3.Scale(dir, dir);
        Vector3 initialScale = Vector3.Scale(trf.localScale, tmp);
        float scaleIncrement = extrusion * extrusionSpeed * Time.deltaTime;
        scaleIncrement += initialScale.magnitude;
        // scaleIncrement�� ��ȭ�� �� scale�� ���� 

        scaleIncrement = Mathf.Clamp(scaleIncrement, minScale, maxScale);
        Vector3 newScale = tmp * scaleIncrement;

        trf.localScale = Vector3.Scale(Vector3.one - tmp, trf.localScale) + newScale;

        // ���� ���� �������� extrude �Ϸ��� position�� �ݴ�� �������� �Ѵ�
        if (dir.x < 0 || dir.y < 0 || dir.z < 0)
            trf.localPosition -= (newScale - initialScale) / 2.0f;
        else
            trf.localPosition += (newScale - initialScale) / 2.0f;
    }

    void UpdateMoving()
    {
        Vector3 dir = new Vector3(_destPos.x, 0, _destPos.z);
        float extension = 0.0f;
        if (Mathf.Abs(dir.magnitude - hook.localPosition.z) > 0.1f)
        {
            if (dir.magnitude > hook.localPosition.z)
                extension = 1.0f;
            else if (dir.magnitude < hook.localPosition.z)
                extension = -1.0f;
        }
        head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.LookRotation(dir), 0.01f);

        // hook�� rope�� position�� arm�� extrusion ������ ���� �����ؾ� �Ѵ�
        Extrude(arm, extension, extensionSpeed * 4, Vector3.forward, armMinScale, armMaxScale);
        hook.localPosition = new Vector3(hook.localPosition.x, hook.localPosition.y, arm.localScale.z + 1.5f);
        rope.localPosition = new Vector3(rope.localPosition.x, rope.localPosition.y, arm.localScale.z + 1.5f);
    }


    void UpdateDropping()
    {
        Vector3 bottom = hook.position;
        if (hasContainer)
            bottom += Vector3.down * containerSize;
        
        RaycastHit hit;
        Physics.Raycast(bottom, -hook.up, out hit, 30.0f);
        Debug.DrawRay(bottom, hit.point, Color.red);
        Debug.Log("Hit Distance: " + hit.distance);
        if (hit.distance > 0.1f)
        {
            Extrude(rope, 1.0f, extensionSpeed, Vector3.down, ropeMinScale, ropeMaxScale);
            hook.localPosition = new Vector3(hook.localPosition.x, -rope.localScale.y - 0.3f, hook.localPosition.z);
        }
        else
        {
            _state = CraneState.Moving;
        }
    }

    void UpdateRaising()
    {
        if (rope.localScale.y - ropeMinScale > 0.1f)
        {
            Extrude(rope, -1.0f, extensionSpeed, Vector3.down, ropeMinScale, ropeMaxScale);
            hook.localPosition = new Vector3(hook.localPosition.x, -rope.localScale.y - 0.3f, hook.localPosition.z);
        }
        else
        {
            _state = CraneState.Moving;
        }
    }

    IEnumerator grabContainer(Transform trf)
    {
        _state = CraneState.Dropping;
        while (_state == CraneState.Dropping)
        {
            yield return null;
        }
        AttachContainer(trf);
        _state = CraneState.Raising;
        while (_state == CraneState.Raising)
        {
            yield return null;
        }
        hasContainer = true;
        _state = CraneState.Moving;
    }

    IEnumerator releaseContainer()
    {
        _state = CraneState.Dropping;
        while (_state == CraneState.Dropping)
        {
            yield return null;
        }
        DetachContainer();
        _state = CraneState.Raising;
        while (_state == CraneState.Raising)
        {
            yield return null;
        }
        hasContainer = false;
        _state = CraneState.Moving;
    }

    void UpdateSpinning()
    {
        if (Input.GetKey(KeyCode.C))
        {
            // hook.localRotation *= Quaternion.Euler(0.0f, 1.1f, 0.0f);
            hook.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
        else if (Input.GetKey(KeyCode.V))
        {
            // hook.localRotation *= Quaternion.Euler(0.0f, -1.1f, 0.0f);
            hook.Rotate(Vector3.down, rotationSpeed * Time.deltaTime);
        }
    }

    void AttachContainer(Transform trf)
    {
        container = trf;
        container.SetParent(hook);
        Debug.Log("Connected");
    }

    void DetachContainer()
    {
        container.SetParent(null);
        container = null;
        Debug.Log("Disconnected");
    }

    // Update is called once per frame
    void Update()
    {
        Checking();
        OnKeyPressed();
        onMouseMove();
        OnMouseClicked();
        switch (_state)
        {
            case CraneState.Moving:
                UpdateMoving(); 
                UpdateSpinning(); break;
            case CraneState.Dropping:
                UpdateDropping(); break;
            case CraneState.Raising:
                UpdateRaising(); break;
            default: break;
        }
    }

    void OnKeyPressed()
    {
        if (_state == CraneState.Moving)
        {
            
            if (Input.GetKey(KeyCode.Z) && isHookOverContainer)
            {
                RaycastHit hit;
                Physics.Raycast(hook.position, -hook.up, out hit);
                StartCoroutine(grabContainer(hit.collider.transform));
            }
            else if (Input.GetKey(KeyCode.X) && hasContainer)
            {
                StartCoroutine(releaseContainer());
            }
        }
    }

    void Checking()
    {
        RaycastHit hit;
        if (Physics.Raycast(hook.position, -hook.up, out hit))
        {
            if (hit.collider != null && hit.collider.gameObject.name == "Container")
            {
                isHookOverContainer = true;
                Debug.Log("Find Object: " + hit.collider.gameObject.name);
                // Debug.Log("Hit Distance: " + hit.distance);
            }
            else
            {
                isHookOverContainer = false;
            }
        }
    }

    void onMouseMove()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (_state == CraneState.Moving && mouseX != 0 && mouseY != 0)
        {
            //Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            //RaycastHit hit;
            //if (Physics.Raycast(ray, out hit, 100.0f))
            //{
            //    _destPos = new Vector3(hit.point.x, 0, hit.point.z);
            //}
        }
    }

    void OnMouseClicked()
    {
        if (Input.GetMouseButtonDown(0) && !hasHit)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100.0f))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    isSelected ^= true;
                    if (isSelected)
                        _state = CraneState.Moving;
                    else
                        _state = CraneState.Stopped;

                    hasHit = true;
                    StartCoroutine(ResetHitFlagAfterSeconds(0.5f));
                    Debug.Log($"Selected : {isSelected}");
                }
                else if (isSelected)
                {
                    _destPos = hit.point;
                    // Debug.Log($"Hit : {_destPos}");
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (isSelected && container != null)
                DetachContainer();
        }
    }

    IEnumerator ResetHitFlagAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        hasHit = false;
    }
}
