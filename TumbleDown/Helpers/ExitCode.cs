// Copyright 2018 Louis S.Berman.
//
// This file is part of TumbleDown.
//
// TumbleDown is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation, either version 3 of the License, 
// or (at your option) any later version.
//
// TumbleDown is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU 
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TumbleDown.  If not, see <http://www.gnu.org/licenses/>.

namespace TumbleDown
{
    public static class ExitCode
    {
        public const int Success = 0;
        public const int NoArgs = -1;
        public const int NoBlogName = -2;
        public const int BadBlogName = -3;
        public const int ParseError = -4;
        public const int BadThreads = -5;
        public const int RuntimeError = -6;
    }
}
