using System.Collections.Generic;
using UnityEngine;

public class CircleGestureDetector
{
    private List<Vector2> touchPositions = new List<Vector2>();
    private bool isDrawing = false;
    private float errorRange = 200f;

    private int _clickCount;
    private float _lastClickTime;
    private float _doubleClickInternal = 0.5f;

    private bool _isCircle;

    public bool Update()
    {
        if (Application.isEditor)
        {
            InEditor();
        }
        else
        {
            InAndroid();
        }

        return _isCircle;
    }

    private void InAndroid()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (touch.tapCount == 3)
                    {
                        touchPositions.Clear();
                        isDrawing = true;
                        touchPositions.Add(touch.position);
                    }
                    break;

                case TouchPhase.Moved:
                    if (isDrawing)
                    {
                        var distance = Vector2.Distance(touch.position, touchPositions[touchPositions.Count - 1]);
                        if (distance > 5f)
                        {
                            touchPositions.Add(touch.position);
                        }
                    }
                    break;

                case TouchPhase.Ended:
                    if (isDrawing)
                    {
                        if (IsCircle(touchPositions))
                        {
                            _isCircle = true;
                        }
                        isDrawing = false;
                    }
                    break;
            }
        }
    }

    private void InEditor()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time - _lastClickTime < _doubleClickInternal)
            {
                _clickCount++;
                if (_clickCount == 2)
                {
                    _clickCount = 0;
                    touchPositions.Clear();
                    isDrawing = true;
                    touchPositions.Add(Input.mousePosition);
                }
            }

            _lastClickTime = Time.time;
        }

        if (Input.GetMouseButton(0))
        {
            if (isDrawing)
            {
                var mousePos = Input.mousePosition;
                var distance = Vector2.Distance(mousePos, touchPositions[touchPositions.Count - 1]);
                if (distance > 5f)
                {
                    touchPositions.Add(mousePos);
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDrawing)
            {
                if (IsCircle(touchPositions))
                {
                    _isCircle = true;
                }

                isDrawing = false;
            }
        }
    }

    private bool IsCircle(List<Vector2> points)
    {
        if (points.Count < 5)
        {
            return false;
        }

        //var leadDistance = Vector2.Distance(points[0], points[points.Count - 1]);
        //if (leadDistance > 100f)
        //{
        //    return false;
        //}

        //center
        Vector2 center = CalculateCircleCenter(points);

        //radius
        float radius = 0f;
        foreach (var p in points)
        {
            radius += Vector2.Distance(p, center);
        }
        radius /= points.Count;

        //check
        foreach (Vector2 p in points)
        {
            float distance = Vector2.Distance(p, center);
            var dif = Mathf.Abs(distance - radius);
            if (dif > errorRange)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 CalculateCircleCenter(List<Vector2> points)
    {
        Vector2 center = Vector2.zero;
        foreach (Vector2 point in points)
        {
            center += point;
        }
        center /= points.Count;
        return center;
    }

    public void Reset()
    {
        _isCircle = false;
        _lastClickTime = 0f;
        _clickCount = 0;
    }
}