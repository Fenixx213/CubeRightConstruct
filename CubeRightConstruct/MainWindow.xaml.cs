using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace CubeRightConstruct
{
    public partial class MainWindow : Window
    {
        private Random _random = new Random();
        private int _cubeCount;
        private List<Point3D> _cubePositions = new List<Point3D>();
        private ModelVisual3D _cubeVisual;
        private Canvas _drawingCanvas;
        private List<Button> _optionButtons = new List<Button>();
        private HashSet<Point> _correctPattern;
        private string _correctView;
        private List<string> _views = new List<string> { "Top View", "Left View", "Front View" };
        private bool _isProcessing = false;
        private bool _isDragging = false;
        private Point _lastMousePosition;
        private double _theta = Math.PI / 4; // Azimuthal angle (horizontal rotation)
        private double _phi = Math.PI / 4;   // Polar angle (vertical rotation)
        private double _radius = 7.071;      // Distance from camera to target (sqrt(5*5 + 5*5))
        private Point3D _target = new Point3D(0, 0, 0); // Camera orbits around this point

        public MainWindow()
        {
            InitializeComponent();

            _drawingCanvas = new Canvas { Width = 420, Height = 420, Background = Brushes.White };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            stackPanel.Children.Add(new TextBlock { Text = "Choose the correct 2D view:", Margin = new Thickness(5) });
            stackPanel.Children.Add(_drawingCanvas);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            var clearButton = new Button { Content = "Clear" };
            clearButton.Click += ClearGrid_Click;
            
            buttonPanel.Children.Add(clearButton);
         
            stackPanel.Children.Add(buttonPanel);

            var viewport = new Viewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = new Point3D(5, 5, 5),
                    LookDirection = new Vector3D(-5, -5, -5),
                    UpDirection = new Vector3D(0, 1, 0),
                    FieldOfView = 60
                },
                Margin = new Thickness(10)
            };

            // Add mouse event handlers for camera control
            viewport.MouseLeftButtonDown += Viewport_MouseLeftButtonDown;
            viewport.MouseMove += Viewport_MouseMove;
            viewport.MouseLeftButtonUp += Viewport_MouseLeftButtonUp;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            grid.Children.Add(viewport);
            Grid.SetColumn(stackPanel, 1);
            grid.Children.Add(stackPanel);

            Content = grid;

            SetupLighting(viewport);
            GenerateCubes(viewport);
            ChooseRandomView();
            DrawViewOptions();
        }

        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isProcessing)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition((Viewport3D)sender);
                ((Viewport3D)sender).CaptureMouse();
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is Viewport3D viewport)
            {
                var currentPosition = e.GetPosition(viewport);
                var delta = currentPosition - _lastMousePosition;

                // Update angles based on mouse movement
                _theta -= delta.X * 0.01; // Horizontal rotation
                _phi = Math.Max(0.1, Math.Min(Math.PI - 0.1, _phi - delta.Y * 0.01)); // Vertical rotation, clamped to avoid flipping

                // Update camera position using spherical coordinates
                var camera = (PerspectiveCamera)viewport.Camera;
                camera.Position = new Point3D(
                    _target.X + _radius * Math.Sin(_phi) * Math.Cos(_theta),
                    _target.Y + _radius * Math.Cos(_phi),
                    _target.Z + _radius * Math.Sin(_phi) * Math.Sin(_theta)
                );
                camera.LookDirection = _target - camera.Position;
                camera.UpDirection = new Vector3D(0, 1, 0);

                _lastMousePosition = currentPosition;
            }
        }

        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ((Viewport3D)sender).ReleaseMouseCapture();
            }
        }

        private void SetupLighting(Viewport3D viewport)
        {
            Model3DGroup modelGroup = new Model3DGroup();
            modelGroup.Children.Add(new AmbientLight(Color.FromRgb(255, 255, 255)));
            ModelVisual3D lighting = new ModelVisual3D();
            lighting.Content = modelGroup;
            viewport.Children.Add(lighting);
        }

        private void GenerateCubes(Viewport3D viewport)
        {
            _cubeCount = _random.Next(3, 6);
            _cubePositions.Clear();

            var cubeGroup = new Model3DGroup();
            var occupiedPositions = new List<Point3D> { new Point3D(0, 0, 0) };
            _cubePositions.Add(new Point3D(0, 0, 0));

            // Добавляем первый куб и его wireframe
            cubeGroup.Children.Add(CreateCube(0, 0, 0));
            var wireframe0 = CreateCubeWireframe();
            wireframe0.Transform = new TranslateTransform3D(0.5, 0.5, 0.5);
            cubeGroup.Children.Add(wireframe0);

            var possiblePositions = new List<Point3D>();
            AddPossiblePositions(new Point3D(0, 0, 0), possiblePositions, occupiedPositions);

            for (int i = 1; i < _cubeCount; i++)
            {
                if (possiblePositions.Count == 0) break;
                int index = _random.Next(possiblePositions.Count);
                var newPosition = possiblePositions[index];
                possiblePositions.RemoveAt(index);

                cubeGroup.Children.Add(CreateCube(newPosition.X, newPosition.Y, newPosition.Z));

                var wireframe = CreateCubeWireframe();
                wireframe.Transform = new TranslateTransform3D(newPosition.X + 0.5, newPosition.Y + 0.5, newPosition.Z + 0.5);
                cubeGroup.Children.Add(wireframe);

                occupiedPositions.Add(newPosition);
                _cubePositions.Add(newPosition);
                AddPossiblePositions(newPosition, possiblePositions, occupiedPositions);
            }

            if (_cubeVisual != null)
            {
                viewport.Children.Remove(_cubeVisual);
            }
            _cubeVisual = new ModelVisual3D { Content = cubeGroup };
            viewport.Children.Add(_cubeVisual);

            UpdateCameraTarget();
        }


        private void UpdateCameraTarget()
        {
            if (_cubePositions.Count > 0)
            {
                double avgX = _cubePositions.Average(p => p.X);
                double avgY = _cubePositions.Average(p => p.Y);
                double avgZ = _cubePositions.Average(p => p.Z);
                _target = new Point3D(avgX + 0.5, avgY + 0.5, avgZ + 0.5); // Center of cubes
                var camera = (PerspectiveCamera)((Viewport3D)VisualTreeHelper.GetChild((Grid)Content, 0)).Camera;
                camera.LookDirection = _target - camera.Position;
            }
        }

        private void AddPossiblePositions(Point3D position, List<Point3D> possiblePositions, List<Point3D> occupiedPositions)
        {
            double cubeSize = 1.0;
            var directions = new[]
            {
                new Point3D(position.X + cubeSize, position.Y, position.Z),
                new Point3D(position.X - cubeSize, position.Y, position.Z),
                new Point3D(position.X, position.Y + cubeSize, position.Z),
                new Point3D(position.X, position.Y - cubeSize, position.Z),
                new Point3D(position.X, position.Y, position.Z + cubeSize),
                new Point3D(position.X, position.Y, position.Z - cubeSize)
            };
            foreach (var pos in directions)
            {
                if (!occupiedPositions.Contains(pos) && !possiblePositions.Contains(pos))
                    possiblePositions.Add(pos);
            }
        }

        private GeometryModel3D CreateCube(double x, double y, double z)
        {
            var mesh = new MeshGeometry3D();
            double halfSize = 0.5;
            double centerX = x + halfSize;
            double centerY = y + halfSize;
            double centerZ = z + halfSize;

            var points = new Point3DCollection
    {
        new Point3D(centerX - halfSize, centerY - halfSize, centerZ - halfSize),
        new Point3D(centerX + halfSize, centerY - halfSize, centerZ - halfSize),
        new Point3D(centerX + halfSize, centerY + halfSize, centerZ - halfSize),
        new Point3D(centerX - halfSize, centerY + halfSize, centerZ - halfSize),
        new Point3D(centerX - halfSize, centerY - halfSize, centerZ + halfSize),
        new Point3D(centerX + halfSize, centerY - halfSize, centerZ + halfSize),
        new Point3D(centerX + halfSize, centerY + halfSize, centerZ + halfSize),
        new Point3D(centerX - halfSize, centerY + halfSize, centerZ + halfSize)
    };

            var indices = new Int32Collection
    {
        0,1,2, 0,2,3,
        4,6,5, 4,7,6,
        0,3,7, 0,7,4,
        1,5,6, 1,6,2,
        3,2,6, 3,6,7,
        0,4,5, 0,5,1
    };

            mesh.Positions = points;
            mesh.TriangleIndices = indices;

            var color = Color.FromRgb((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(color)),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
            };
        }


        private void DrawViewOptions()
        {
            _drawingCanvas.Children.Clear();
            _optionButtons.Clear();

            _correctPattern = GetViewPattern(_correctView);
            var options = GenerateOptions(_correctPattern);

            while (options.Count < 4)
            {
                HashSet<Point> incorrect = GenerateIncorrectOption(_correctPattern);
                if (!options.Any(option => option.SetEquals(incorrect)))
                {
                    options.Add(incorrect);
                }
            }

            options = options.OrderBy(x => _random.Next()).ToList();

            // Add view label
            TextBlock viewLabel = new TextBlock
            {
                Text = $"View: {_correctView}",
                Foreground = Brushes.Black,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _drawingCanvas.Children.Add(viewLabel);
            Canvas.SetTop(viewLabel, 0);
            Canvas.SetLeft(viewLabel, _drawingCanvas.Width / 2 - viewLabel.Text.Length * 4);

            for (int i = 0; i < 4; i++)
            {
                int localIndex = i;
                var button = new Button
                {
                    Content = CreateOptionCanvas(options[localIndex]),
                    Width = 100,
                    Height = 100,
                    Margin = new Thickness(5),
                    Tag = options[localIndex]
                };
                button.Click -= CheckAnswer;
                button.Click += CheckAnswer;
                _optionButtons.Add(button);
                _drawingCanvas.Children.Add(button);
            }

            Canvas.SetLeft(_optionButtons[0], 0);
            Canvas.SetTop(_optionButtons[0], 30);
            Canvas.SetLeft(_optionButtons[1], 210);
            Canvas.SetTop(_optionButtons[1], 30);
            Canvas.SetLeft(_optionButtons[2], 0);
            Canvas.SetTop(_optionButtons[2], 240);
            Canvas.SetLeft(_optionButtons[3], 210);
            Canvas.SetTop(_optionButtons[3], 240);
        }

        private HashSet<Point> GenerateIncorrectOption(HashSet<Point> correctPattern)
        {
            HashSet<Point> incorrectPattern = new HashSet<Point>(correctPattern);
            if (incorrectPattern.Count > 1)
            {
                incorrectPattern.Remove(incorrectPattern.First());
            }
            else
            {
                incorrectPattern.Add(new Point(_random.Next(3), _random.Next(3)));
            }
            return incorrectPattern;
        }
        private GeometryModel3D CreateCubeWireframe()
        {
            MeshGeometry3D wireframeMesh = new MeshGeometry3D();
            double size = 0.5; // Cube size (same as main cube)
            double thickness = 0.015; // Thickness of the wireframe edges (reduced to avoid overlap)

            // Define the 8 vertices of the cube
            Point3D[] vertices = new Point3D[]
            {
                new Point3D(-size, -size, -size),
                new Point3D(size, -size, -size),
                new Point3D(size, size, -size),
                new Point3D(-size, size, -size),
                new Point3D(-size, -size, size),
                new Point3D(size, -size, size),
                new Point3D(size, size, size),
                new Point3D(-size, size, size)
            };

            // Define the 12 edges of the cube by connecting vertices
            int[][] edges = new int[][]
            {
                new[] { 0, 1 }, // Bottom front
                new[] { 1, 2 }, // Bottom right
                new[] { 2, 3 }, // Bottom back
                new[] { 3, 0 }, // Bottom left
                new[] { 4, 5 }, // Top front
                new[] { 5, 6 }, // Top right
                new[] { 6, 7 }, // Top back
                new[] { 7, 4 }, // Top left
                new[] { 0, 4 }, // Front left vertical
                new[] { 1, 5 }, // Front right vertical
                new[] { 2, 6 }, // Back right vertical
                new[] { 3, 7 }  // Back left vertical
            };

            Point3DCollection positions = new Point3DCollection();
            Int32Collection indices = new Int32Collection();

            // For each edge, create a thin rectangular prism (approximating a line)
            for (int i = 0; i < edges.Length; i++)
            {
                Point3D start = vertices[edges[i][0]];
                Point3D end = vertices[edges[i][1]];
                Vector3D dir = end - start;
                double length = dir.Length;
                dir.Normalize();

                // Define a small cross-section perpendicular to the edge direction
                Vector3D up = Math.Abs(dir.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
                Vector3D right = Vector3D.CrossProduct(dir, up);
                right.Normalize();
                up = Vector3D.CrossProduct(right, dir);
                up.Normalize();

                // Create 4 vertices for the cross-section at start and end
                Point3D[] crossSectionStart = new Point3D[4];
                Point3D[] crossSectionEnd = new Point3D[4];
                for (int j = 0; j < 4; j++)
                {
                    double angle = j * Math.PI / 2;
                    Vector3D offset = (Math.Cos(angle) * right + Math.Sin(angle) * up) * thickness;
                    crossSectionStart[j] = start + offset;
                    crossSectionEnd[j] = end + offset;
                }

                // Add vertices to positions
                int baseIndex = positions.Count;
                foreach (var p in crossSectionStart) positions.Add(p);
                foreach (var p in crossSectionEnd) positions.Add(p);

                // Define triangles for the prism (4 sides, each with 2 triangles)
                int[] sideIndices = new int[]
                {
                    0, 1, 5,  0, 5, 4, // Side 1
                    1, 2, 6,  1, 6, 5, // Side 2
                    2, 3, 7,  2, 7, 6, // Side 3
                    3, 0, 4,  3, 4, 7  // Side 4
                };
                foreach (int idx in sideIndices)
                {
                    indices.Add(baseIndex + idx);
                }
            }

            wireframeMesh.Positions = positions;
            wireframeMesh.TriangleIndices = indices;

            // Create black material for the wireframe
            GeometryModel3D wireframeModel = new GeometryModel3D
            {
                Geometry = wireframeMesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0))),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0)))
            };

            return wireframeModel;
        }
        private Canvas CreateOptionCanvas(HashSet<Point> pattern)
        {
            var canvas = new Canvas { Width = 200, Height = 200, Background = Brushes.White };
            foreach (var point in pattern)
            {
                var rect = new Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = Brushes.Yellow,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, point.X * 20);
                Canvas.SetTop(rect, point.Y * 20);
                canvas.Children.Add(rect);
            }
            return canvas;
        }

        private HashSet<Point> GetViewPattern(string view)
        {
            var pattern = new HashSet<Point>();
            double minX = _cubePositions.Min(p => p.X);
            double minY = _cubePositions.Min(p => p.Y);
            double minZ = _cubePositions.Min(p => p.Z);
            double maxX = _cubePositions.Max(p => p.X);

            if (view == "Top View")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.X, p.Z }).Select(g => g.OrderByDescending(p => p.Y).First());
                foreach (var pos in grouped)
                    pattern.Add(new Point((int)(pos.X - minX), (int)(pos.Z - minZ)));
            }
            else if (view == "Left View")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.Y, p.Z }).Select(g => g.OrderByDescending(p => p.X).First());
                foreach (var pos in grouped)
                {
                    int z = (int)(pos.Z - minZ);
                    int y = (int)(pos.Y - minY);
                    pattern.Add(new Point(2 - z, 2 - y));
                }
            }
            else if (view == "Front View")
            {
                var grouped = _cubePositions.GroupBy(p => new { p.X, p.Y }).Select(g => g.OrderByDescending(p => p.Z).First());
                double maxHeight = _cubePositions.Max(p => p.Y) - minY;
                foreach (var pos in grouped)
                    pattern.Add(new Point((int)(pos.X - minX), (int)(maxHeight - (pos.Y - minY))));
            }

            return pattern;
        }

        private List<HashSet<Point>> GenerateOptions(HashSet<Point> correct)
        {
            var options = new List<HashSet<Point>> { new HashSet<Point>(correct) };
            while (options.Count < 4)
            {
                var incorrect = new HashSet<Point>(correct);
                if (incorrect.Count > 0)
                {
                    var toRemove = correct.ElementAt(_random.Next(correct.Count));
                    incorrect.Remove(toRemove);
                }
                if (incorrect.Count > 0 && !options.Contains(incorrect))
                {
                    options.Add(incorrect);
                }
            }
            return options;
        }

        private void ChooseRandomView()
        {
            int index = _random.Next(_views.Count);
            _correctView = _views[index];
        }

        private void CheckAnswer(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;

            if (sender is Button button && button.Tag is HashSet<Point> selectedPattern)
            {
                bool isCorrect = _correctPattern.SetEquals(selectedPattern);
                if (isCorrect)
                {
                    MessageBox.Show("Correct!");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Restart();
                        _isProcessing = false;
                    }));
                }
                else
                {
                    MessageBox.Show($"Incorrect. The correct view is {_correctView}. Try again!");
                    _isProcessing = false;
                }
            }
            else
            {
                _isProcessing = false;
            }
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Restart();
                _isProcessing = false;
            }));
        }

    

        private double maxY(IEnumerable<Point3D> points)
        {
            double minY = points.Min(p => p.Y);
            return points.Max(p => p.Y - minY);
        }

        private void Restart()
        {
            var viewport = (Viewport3D)VisualTreeHelper.GetChild((Grid)Content, 0);

            if (_cubeVisual != null)
            {
                viewport.Children.Remove(_cubeVisual);
                _cubeVisual = null;
            }

            GenerateCubes(viewport);
            ChooseRandomView();
            DrawViewOptions();
        }
    }
}