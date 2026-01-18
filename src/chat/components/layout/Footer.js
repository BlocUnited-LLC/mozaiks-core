import React from "react";

const Footer = () => {
  return (
    <React.Fragment>
      <div
        className="hidden lg:flex flex-row justify-between items-center w-[100%] lg:px-[200px] px-[20px] h-[44px] mt-2"
        style={{
          //   background: "rgba(0, 0, 0, 0.4)",
          backdropFilter: "blur(6px)",
        }}
      >
        <p className=" flex flex-col justify-center  border-[#ffffffff] text-white md:text-[15px] text-[12px]  leading-[15px]  oxanium  font-[400] text-center">
          Legal Notice
        </p>
        <p className=" flex flex-col justify-center  border-[#ffffffff] text-white md:text-[15px] text-[12px]   leading-[15px] oxanium  font-[400] text-center">
          Terms of Service
        </p>
        <p className=" flex flex-col justify-center  border-[#ffffffff] text-white md:text-[15px] text-[12px]   leading-[15px]  oxanium  font-[400] text-center">
          Cookie Policy
        </p>
      </div>
    </React.Fragment>
  );
};

export default Footer;
