FROM ubuntu:20.04

RUN apt update && \
    DEBIAN_FRONTEND=noninteractive apt install -y git wget software-properties-common && \
    add-apt-repository ppa:ubuntu-toolchain-r/test -y && \
    apt update && \
    apt install gcc-13 g++-13 gcc-11 g++-11 clang-10 clang-11 gfortran-11 gfortran-13 -y

RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update; \
    DEBIAN_FRONTEND=noninteractive TZ=Etc/UTC apt-get install -y dotnet-sdk-7.0

RUN apt install python3-pip -y && \
    pip install pyelftools

RUN mkdir -p /project/B2R2

COPY ./B2R2 /project/B2R2

RUN cd /project/B2R2 && \
    dotnet build -c Release && \
    dotnet build -c Debug

RUN mkdir -p /project/superSymbolizer
COPY ./superSymbolizer /project/superSymbolizer

RUN apt install time flex -y

RUN wget https://ftp.gnu.org/gnu/coreutils/coreutils-9.1.tar.gz && \
    tar -xzf coreutils-9.1.tar.gz

RUN cd /coreutils-9.1 && \
    FORCE_UNSAFE_CONFIGURE=1 ./configure && \
    make

COPY ./build_script/script/coreutils_copy.sh /coreutils-9.1/copy.sh
COPY ./build_script/script/coreutils-9.1_list.txt /coreutils-9.1/coreutils-9.1_list.txt

RUN wget https://ftp.gnu.org/gnu/binutils/binutils-2.40.tar.gz && \
    tar -xzf binutils-2.40.tar.gz

RUN apt install texinfo bison dejagnu -y  && \
    cd /binutils-2.40 && \
    ./configure && \
    make

COPY ./build_script/script/binutils_copy.sh /binutils-2.40/copy.sh
COPY ./build_script/script/binutils-2.40_list.txt /binutils-2.40/binutils-2.40_list.txt

RUN wget --no-check-certificate -O - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add - && \
    add-apt-repository 'deb http://apt.llvm.org/focal/   llvm-toolchain-focal-13  main' && \
    apt update && \
    apt install -y clang-13 libomp-dev && \
    apt install -y libomp-13-dev

RUN mkdir -p /project && \
    cd /project && \
    git clone https://github.com/SoftSec-KAIST/Reassessor.git && \
    cd /project/Reassessor && \
    git checkout suri && \
    pip install -r requirements.txt && \
    python3 setup.py install

RUN mkdir -p /data3/3_supersetCFG/benchmark/ && \
    mkdir -p /data3/3_supersetCFG/benchmark_no_ehframe/

RUN git clone https://github.com/witbring/retrowrite_ef4e541.git /project/retrowrite && \
    cd /project/retrowrite && \
    pip3 install -r requirements.txt

RUN  apt-get install php-cli php-xml -y; \
    git clone https://github.com/phoronix-test-suite/phoronix-test-suite.git; \
    cd phoronix-test-suite; \
    ./install-sh; \
    phoronix-test-suite install  sqlite; \
    phoronix-test-suite intsall  nginx; \
    phoronix-test-suite install  apache; \
    phoronix-test-suite install compress-7zip; \
    phoronix-test-suite install mysqlslap; \
