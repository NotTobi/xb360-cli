FROM ubuntu:22.04

RUN apt update && apt install -y unzip wget

RUN BINDIR='/opt/xb360/bin' ; TMPFILE=`mktemp` ; mkdir -p $BINDIR; \
    wget https://github.com/NotTobi/xb360-cli/releases/latest/download/linux-x64.zip -O $TMPFILE \
    && unzip $TMPFILE -d $BINDIR ; chmod +x $BINDIR/xb360 ; rm $TMPFILE

ENV PATH="${PATH}:/opt/xb360/bin"

# todo: container