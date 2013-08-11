using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExposureMeter
{
    public class MainViewModel
    {
        public MainViewModel()
        {
        }

        public Camera Camera
        {
            get
            {
                if (m_camera == null)
                    m_camera = new Camera();
                return m_camera;
            }
        }
        private Camera m_camera;
    }
}
